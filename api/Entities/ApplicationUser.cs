using Microsoft.AspNetCore.Identity;

namespace CarWashTicket.Api.Entities;

// Tüm kullanıcılar burada. Global rol Identity'de, istasyon görevi StationStaff'ta.
public class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<StationStaff> StationAssignments { get; set; } = new List<StationStaff>();
}

// Guid anahtarlı Identity için gerekli.
public class ApplicationRole : IdentityRole<Guid>
{
}
