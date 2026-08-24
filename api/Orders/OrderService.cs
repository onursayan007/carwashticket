using CarWashTicket.Api.Data;
using CarWashTicket.Api.Dtos;
using CarWashTicket.Api.Entities;
using CarWashTicket.Api.Payments;
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

public class OrderService(
    AppDbContext db,
    IPaymentProvider paymentProvider,
    OrderStateMachine stateMachine,
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
