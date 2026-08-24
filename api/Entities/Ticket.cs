namespace CarWashTicket.Api.Entities;

// Ödemesi tamamlanan siparişin QR bileti. Bir siparişe en fazla bir bilet.
public class Ticket
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid StationId { get; set; }

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

    public ApplicationUser? RedeemedByUser { get; set; }
}
