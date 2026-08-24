namespace CarWashTicket.Api.Entities;

// Kullanıcı-istasyon bağlantısı. PK: (StationId, UserId).
public class StationStaff
{
    public Guid StationId { get; set; }

    public Guid UserId { get; set; }

    public StationRole Role { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    public Station Station { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
