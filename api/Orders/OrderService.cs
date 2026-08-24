using CarWashTicket.Api.Data;
using CarWashTicket.Api.Dtos;
using CarWashTicket.Api.Entities;
using CarWashTicket.Api.Ledger;
using CarWashTicket.Api.Payments;
using CarWashTicket.Api.Tickets;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarWashTicket.Api.Orders;

public enum OrderCreationOutcome
{
    Created,
    // Aynı Idempotency-Key daha önce kullanılmış, mevcut sipariş dönüldü.
    Replayed,
    ServiceNotFound,
    KeyBelongsToAnotherCustomer,
    PaymentFailed
}

public record OrderCreationResult(
    OrderCreationOutcome Outcome,
    CreateOrderResponse? Response,
    string? Error);

public enum PaymentConfirmationOutcome
{
    Confirmed,
    // Sipariş zaten ödenmiş; tekrar gelen bildirim sessizce yutuluyor.
    AlreadyConfirmed,
    OrderNotFound,
    AmountMismatch,
    InvalidState
}

public class OrderService(
    AppDbContext db,
    IPaymentProvider paymentProvider,
    OrderStateMachine stateMachine,
    LedgerService ledger,
    IConfiguration configuration,
    ILogger<OrderService> logger)
{
    public async Task<OrderCreationResult> CreateAsync(
        Guid customerId,
        string customerEmail,
        CreateOrderRequest request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var existing = await FindByKeyAsync(idempotencyKey, ct);

        if (existing is not null)
        {
            return existing.CustomerId == customerId
                ? Replay(existing)
                : new OrderCreationResult(
                    OrderCreationOutcome.KeyBelongsToAnotherCustomer,
                    null,
                    "Bu Idempotency-Key başka bir kullanıcıya ait.");
        }

        // Kural 8: hizmet daima istasyonla birlikte doğrulanır.
        var service = await db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == request.ServiceId
                     && s.StationId == request.StationId
                     && s.IsActive
                     && s.Station.IsActive,
                ct);

        if (service is null)
        {
            return new OrderCreationResult(
                OrderCreationOutcome.ServiceNotFound,
                null,
                "Hizmet bulunamadı veya aktif değil.");
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            StationId = service.StationId,
            CustomerId = customerId,
            ServiceId = service.Id,
            Amount = service.Price,
            CommissionAmount = CalculateCommission(service.Price),
            IdempotencyKey = idempotencyKey,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(CheckoutTimeoutMinutes),
            CreatedAt = DateTimeOffset.UtcNow
            // Status yazılmıyor: OrderStatus varsayılanı Created ve setter private.
        };

        db.Orders.Add(order);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            // Aynı key ile iki istek aynı anda geldi; index kazananı belirledi.
            db.Entry(order).State = EntityState.Detached;

            var winner = await FindByKeyAsync(idempotencyKey, ct);

            if (winner is null)
            {
                throw;
            }

            logger.LogInformation("Eşzamanlı idempotent istek. Key: {Key}", idempotencyKey);

            return Replay(winner);
        }

        return await StartCheckoutAsync(order, service.Name, customerEmail, ct);
    }

    private async Task<OrderCreationResult> StartCheckoutAsync(
        Order order,
        string serviceName,
        string customerEmail,
        CancellationToken ct)
    {
        var checkout = await paymentProvider.StartCheckoutAsync(
            new CheckoutRequest(
                order.Id,
                order.Amount,
                serviceName,
                customerEmail,
                configuration["Payment:CallbackUrl"] ?? throw new InvalidOperationException(
                    "Payment:CallbackUrl tanımlı değil.")),
            ct);

        if (!checkout.Success)
        {
            stateMachine.Transition(order, OrderStatus.Failed);
            order.FailureReason = checkout.ErrorMessage;
            await db.SaveChangesAsync(ct);

            return new OrderCreationResult(
                OrderCreationOutcome.PaymentFailed,
                null,
                checkout.ErrorMessage ?? "Ödeme başlatılamadı.");
        }

        stateMachine.Transition(order, OrderStatus.AwaitingPayment);
        order.ProviderPaymentId = checkout.ProviderPaymentId;
        order.CheckoutRedirectUrl = checkout.RedirectUrl;
        await db.SaveChangesAsync(ct);

        return new OrderCreationResult(OrderCreationOutcome.Created, ToResponse(order), null);
    }

    // Müşteri sadece kendi siparişini sorgulayabilir.
    public Task<OrderStatusResponse?> GetStatusAsync(
        Guid customerId,
        Guid orderId,
        CancellationToken ct = default)
        => db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId && o.CustomerId == customerId)
            .Select(o => new OrderStatusResponse(o.Id, o.Status, o.Amount))
            .FirstOrDefaultAsync(ct);

    // Ödemeyi kesinleştirir: durum Paid'e geçer, bilet üretilir, defter kayıtları
    // yazılır. Üçü tek SaveChanges ile, yani tek transaction'da commit edilir.
    public async Task<PaymentConfirmationOutcome> ConfirmPaymentAsync(
        Guid orderId,
        string? providerPaymentId,
        decimal? paidAmount,
        CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.Ticket)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order is null)
        {
            return PaymentConfirmationOutcome.OrderNotFound;
        }

        if (order.Status is OrderStatus.Paid or OrderStatus.Redeemed or OrderStatus.Settled)
        {
            return PaymentConfirmationOutcome.AlreadyConfirmed;
        }

        if (!OrderStateMachine.CanTransition(order.Status, OrderStatus.Paid))
        {
            logger.LogWarning(
                "Ödeme onayı reddedildi, sipariş {OrderId} durumu: {Status}", orderId, order.Status);

            return PaymentConfirmationOutcome.InvalidState;
        }

        // Sağlayıcı tutar bildirdiyse siparişteki tutarla birebir eşleşmeli.
        if (paidAmount.HasValue && paidAmount.Value != order.Amount)
        {
            logger.LogError(
                "Tutar uyuşmuyor. Sipariş {OrderId}: beklenen {Expected}, gelen {Received}",
                orderId, order.Amount, paidAmount.Value);

            return PaymentConfirmationOutcome.AmountMismatch;
        }

        stateMachine.Transition(order, OrderStatus.Paid);

        if (providerPaymentId is not null)
        {
            order.ProviderPaymentId = providerPaymentId;
        }

        db.Tickets.Add(BuildTicket(order));
        ledger.AddPaymentEntries(order);

        await db.SaveChangesAsync(ct);

        return PaymentConfirmationOutcome.Confirmed;
    }

    public async Task MarkPaymentFailedAsync(Guid orderId, string? reason, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order is null || !OrderStateMachine.CanTransition(order.Status, OrderStatus.Failed))
        {
            return;
        }

        stateMachine.Transition(order, OrderStatus.Failed);
        order.FailureReason = reason;

        await db.SaveChangesAsync(ct);
    }

    private Ticket BuildTicket(Order order)
    {
        var now = DateTimeOffset.UtcNow;

        return new Ticket
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            StationId = order.StationId,
            Code = TicketService.GenerateCode(),
            Status = TicketStatus.Issued,
            IssuedAt = now,
            ExpiresAt = now.AddDays(configuration.GetValue("Ticket:ValidDays", 30))
        };
    }

    private Task<Order?> FindByKeyAsync(string idempotencyKey, CancellationToken ct)
        => db.Orders.FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);

    private decimal CalculateCommission(decimal amount)
    {
        var rate = configuration.GetValue("Payment:CommissionRate", 0m);

        if (rate is < 0m or > 1m)
        {
            throw new InvalidOperationException(
                $"Payment:CommissionRate 0 ile 1 arasında olmalı, gelen değer: {rate}.");
        }

        return Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
    }

    private int CheckoutTimeoutMinutes => configuration.GetValue("Payment:CheckoutTimeoutMinutes", 20);

    private static OrderCreationResult Replay(Order order)
        => new(OrderCreationOutcome.Replayed, ToResponse(order), null);

    private static CreateOrderResponse ToResponse(Order order)
        => new(order.Id, order.Status, order.Amount, order.CheckoutRedirectUrl);

    private static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
