namespace CarWashTicket.Api.Entities;

// Kiracı kökü. Her kayıt bir istasyona bağlıdır.
public class Station
{
    public Guid Id { get; set; }

    // Müşteriye görünen ad, örn. "Elmalı Petrol Self Servis".
    public string Name { get; set; } = null!;

    public StationType Type { get; set; }

    // --- Konum ---
    public string? Address { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }

    // Harita ve mesafe sıralaması için. WGS84.
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    // --- Ticari bilgiler (admin girer) ---
    public string? CompanyName { get; set; }

    public string? TaxNumber { get; set; }

    public string? TaxOffice { get; set; }

    public string? ContactEmail { get; set; }

    public string? PhoneNumber { get; set; }

    // --- Değerlendirme özeti ---
    // Reviews üzerinden hesaplanıp burada tutulur; her listelemede ortalama
    // almak yerine yazma anında güncellenir.
    public decimal RatingAverage { get; set; }

    public int RatingCount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Service> Services { get; set; } = new List<Service>();

    public ICollection<StationStaff> Staff { get; set; } = new List<StationStaff>();
}
