namespace CarWashTicket.Api.Entities;

// Çift kayıt satırı. Her hareket aynı TransactionId altında Debit + Credit olarak yazılır.
public class LedgerEntry
{
    public Guid Id { get; set; }

    public Guid StationId { get; set; }

    // Aynı hareketin satırlarını gruplar.
    public Guid TransactionId { get; set; }

    public Guid? OrderId { get; set; }

    public LedgerAccount Account { get; set; }

    public LedgerDirection Direction { get; set; }

    // Daima pozitif; yön Direction'da.
    public decimal Amount { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Station Station { get; set; } = null!;

    public Order? Order { get; set; }
}
