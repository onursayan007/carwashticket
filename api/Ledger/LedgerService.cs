using CarWashTicket.Api.Data;
using CarWashTicket.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Ledger;

// Çift kayıt kuralı: bir para hareketinin tüm satırları aynı TransactionId altında
// yazılır ve Debit toplamı Credit toplamına eşit olur.
//
// Metotlar SaveChanges ÇAĞIRMAZ; satırlar context'e eklenir ve çağıran, sipariş
// durum değişikliğiyle aynı transaction'da commit eder. Aksi halde "durum değişti
// ama defter yazılmadı" hali oluşabilir.
public class LedgerService(AppDbContext db, ILogger<LedgerService> logger)
{
    // Müşteri ödemesi sağlayıcıda toplanır, karşılığında istasyon hakedişi ve
    // platform komisyonu borç olarak yazılır.
    public void AddPaymentEntries(Order order)
    {
        if (order.Amount <= 0m)
        {
            throw new InvalidOperationException($"Sipariş {order.Id} tutarı sıfır veya negatif.");
        }

        var stationShare = order.Amount - order.CommissionAmount;

        if (stationShare < 0m)
        {
            throw new InvalidOperationException(
                $"Sipariş {order.Id} komisyonu tutarından büyük.");
        }

        var transactionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var entries = new List<LedgerEntry>
        {
            Entry(order, transactionId, now, LedgerAccount.PaymentGateway,
                LedgerDirection.Debit, order.Amount, "Müşteri ödemesi"),

            Entry(order, transactionId, now, LedgerAccount.StationRevenue,
                LedgerDirection.Credit, stationShare, "İstasyon hakedişi")
        };

        // Komisyon sıfırsa satır yazmıyoruz; sıfır tutarlı kayıt defteri kirletir.
        if (order.CommissionAmount > 0m)
        {
            entries.Add(Entry(order, transactionId, now, LedgerAccount.PlatformCommission,
                LedgerDirection.Credit, order.CommissionAmount, "Platform komisyonu"));
        }

        Write(order, transactionId, entries, "ödeme");
    }

    // İade: ödeme satırlarının aynısı ters yönde yazılır. Mevcut kayıt silinmez.
    public void AddRefundEntries(Order order)
    {
        var stationShare = order.Amount - order.CommissionAmount;

        var transactionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var entries = new List<LedgerEntry>
        {
            Entry(order, transactionId, now, LedgerAccount.StationRevenue,
                LedgerDirection.Debit, stationShare, "İstasyon hakedişi iadesi"),

            Entry(order, transactionId, now, LedgerAccount.PaymentGateway,
                LedgerDirection.Credit, order.Amount, "Müşteriye iade")
        };

        if (order.CommissionAmount > 0m)
        {
            entries.Add(Entry(order, transactionId, now, LedgerAccount.PlatformCommission,
                LedgerDirection.Debit, order.CommissionAmount, "Komisyon iadesi"));
        }

        Write(order, transactionId, entries, "iade");
    }

    // Bir siparişin kaydedilmiş tüm satırlarının Debit - Credit farkı sıfır olmalı.
    public async Task<bool> IsOrderBalancedAsync(Guid orderId, CancellationToken ct = default)
    {
        var rows = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.OrderId == orderId)
            .Select(e => new { e.Direction, e.Amount })
            .ToListAsync(ct);

        var balance = rows.Sum(r => r.Direction == LedgerDirection.Debit ? r.Amount : -r.Amount);

        if (balance != 0m)
        {
            logger.LogError(
                "Defter dengesiz. Sipariş {OrderId}, fark: {Balance}", orderId, balance);
        }

        return balance == 0m;
    }

    private void Write(Order order, Guid transactionId, List<LedgerEntry> entries, string kind)
    {
        EnsureBalanced(entries, transactionId);

        db.LedgerEntries.AddRange(entries);

        logger.LogInformation(
            "Defter kaydı ({Kind}). Sipariş {OrderId}, hareket {TransactionId}, {Count} satır",
            kind, order.Id, transactionId, entries.Count);
    }

    // Dengesiz bir set asla yazılmasın; bu hata veriye dönüşürse geriye dönük düzeltmesi zor.
    private static void EnsureBalanced(List<LedgerEntry> entries, Guid transactionId)
    {
        var balance = entries.Sum(
            e => e.Direction == LedgerDirection.Debit ? e.Amount : -e.Amount);

        if (balance != 0m)
        {
            throw new InvalidOperationException(
                $"Hareket {transactionId} dengesiz, fark: {balance}.");
        }
    }

    private static LedgerEntry Entry(
        Order order,
        Guid transactionId,
        DateTimeOffset createdAt,
        LedgerAccount account,
        LedgerDirection direction,
        decimal amount,
        string description) => new()
        {
            Id = Guid.NewGuid(),
            StationId = order.StationId,
            OrderId = order.Id,
            TransactionId = transactionId,
            Account = account,
            Direction = direction,
            Amount = amount,
            Description = description,
            CreatedAt = createdAt
        };
}
