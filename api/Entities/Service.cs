namespace CarWashTicket.Api.Entities;

// İstasyonun sunduğu yıkama hizmeti.
public class Service
{
    public Guid Id { get; set; }

    public Guid StationId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    // Yıkamanın tahmini süresi.
    public int DurationMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public Station Station { get; set; } = null!;
}
