using System.Security.Cryptography;
using CarWashTicket.Api.Data;
using CarWashTicket.Api.Entities;
using CarWashTicket.Api.Orders;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace CarWashTicket.Api.Tickets;

public enum RedeemOutcome
{
    Redeemed,
    // Kod yok, süresi dolmuş, zaten kullanılmış ya da personelin istasyonuna ait değil.
    // Bilerek tek sonuç: hangisi olduğunu dışarıya söylemiyoruz.
    NotRedeemable
}

public record RedeemResult(RedeemOutcome Outcome, Ticket? Ticket, string? ServiceName);

public class TicketService(
    AppDbContext db,
    OrderStateMachine stateMachine,
    ILogger<TicketService> logger)
{
    // 16 bayt rastgele -> 32 karakter. Sıralı veya kısa kod üretilirse
    // başkasının bileti tahmin edilebilir.
    public static string GenerateCode()
        => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    public static byte[] CreateQrPng(string code, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(code, QRCodeGenerator.ECCLevel.Q);

        // PngByteQRCode System.Drawing'e bağlı değil, Linux konteynerde de çalışır.
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }

    public async Task<RedeemResult> RedeemAsync(
        string code,
        Guid staffUserId,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Tek UPDATE ile hem sahiplenme hem yetki kontrolü. Önce okuyup sonra
        // yazsaydık iki personel aynı bileti aynı anda kullanabilirdi.
        var affected = await db.Tickets
            .Where(t => t.Code == code
                        && t.Status == TicketStatus.Issued
                        && t.ExpiresAt > now
                        // Personel yalnızca görevli olduğu istasyonun biletini okutabilir.
                        && db.StationStaff.Any(ss => ss.UserId == staffUserId
                                                     && ss.StationId == t.StationId))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.Status, TicketStatus.Redeemed)
                    .SetProperty(t => t.RedeemedAt, now)
                    .SetProperty(t => t.RedeemedByUserId, staffUserId),
                ct);

        if (affected == 0)
        {
            await transaction.RollbackAsync(ct);

            logger.LogInformation("Bilet kullanılamadı. Personel {StaffId}", staffUserId);

            return new RedeemResult(RedeemOutcome.NotRedeemable, null, null);
        }

        var ticket = await db.Tickets
            .Include(t => t.Order)
            .ThenInclude(o => o.Tickets)
            .FirstAsync(t => t.Code == code, ct);

        // Sipariş ancak TÜM biletleri kullanıldığında Redeemed olur.
        // 2 su + 1 köpük siparişinde ilk okutma siparişi kapatmamalı.
        var allRedeemed = ticket.Order.Tickets.All(t => t.Status == TicketStatus.Redeemed);

        if (allRedeemed && OrderStateMachine.CanTransition(ticket.Order.Status, OrderStatus.Redeemed))
        {
            // Kural 2: sipariş durumu state machine üzerinden ilerler.
            stateMachine.Transition(ticket.Order, OrderStatus.Redeemed);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Bilet kullanıldı. Bilet {TicketId}, sipariş {OrderId}, personel {StaffId}",
            ticket.Id, ticket.OrderId, staffUserId);

        return new RedeemResult(RedeemOutcome.Redeemed, ticket, ticket.ServiceName);
    }
}
