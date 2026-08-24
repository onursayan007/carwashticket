using System.ComponentModel.DataAnnotations;
using CarWashTicket.Api.Entities;

namespace CarWashTicket.Api.Dtos;

public record TicketListItemDto(
    Guid Id,
    // QR istemci tarafında bu koddan üretilir.
    string Code,
    TicketStatus Status,
    string ServiceName,
    string StationName,
    decimal Amount,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RedeemedAt);

public record RedeemTicketRequest
{
    [Required, MaxLength(64)]
    public string Code { get; init; } = null!;
}

public record RedeemTicketResponse(
    bool Success,
    string Message,
    string? ServiceName,
    DateTimeOffset? RedeemedAt);
