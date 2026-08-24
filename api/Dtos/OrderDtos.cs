using System.ComponentModel.DataAnnotations;
using CarWashTicket.Api.Entities;

namespace CarWashTicket.Api.Dtos;

public record CreateOrderRequest
{
    [Required]
    public Guid StationId { get; init; }

    [Required]
    public Guid ServiceId { get; init; }
}

public record CreateOrderResponse(
    Guid OrderId,
    OrderStatus Status,
    decimal Amount,
    string? RedirectUrl);

public record OrderStatusResponse(
    Guid OrderId,
    OrderStatus Status,
    decimal Amount);
