namespace CarWashTicket.Api.Entities;

// Yıkama sonrası müşteri değerlendirmesi. Sipariş başına en fazla bir tane.
public class Review
{
    public Guid Id { get; set; }

    public Guid StationId { get; set; }

    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    // 1-5.
    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Station Station { get; set; } = null!;

    public Order Order { get; set; } = null!;

    public ApplicationUser Customer { get; set; } = null!;
}
