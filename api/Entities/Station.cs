namespace CarWashTicket.Api.Entities;

// Kiracı kökü. Her kayıt bir istasyona bağlıdır.
public class Station
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Service> Services { get; set; } = new List<Service>();

    public ICollection<StationStaff> Staff { get; set; } = new List<StationStaff>();
}
