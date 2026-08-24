namespace CarWashTicket.Api.Entities;

// Satın alınan her birim için bir bilet. Tek kullanımlık.
// 2 su + 1 köpük siparişi 3 bilet üretir.
public class Ticket
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid StationId { get; set; }

    // Biletin hangi hizmeti verdiği: su mu, köpük mü, paket mi.
    public Guid ServiceId { get; set; }

    public string ServiceName { get; set; } = null!;

    // QR içine gömülen kod.
    public string Code { get; set; } = null!;

    public TicketStatus Status { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RedeemedAt { get; set; }

    // Bileti okutan personel.
    public Guid? RedeemedByUserId { get; set; }

    public Order Order { get; set; } = null!;

    public Station Station { get; set; } = null!;

    public Service Service { get; set; } = null!;

    public ApplicationUser? RedeemedByUser { get; set; }
}
