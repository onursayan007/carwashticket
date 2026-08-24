using System.ComponentModel.DataAnnotations;

namespace CarWashTicket.Api.Dtos;

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
