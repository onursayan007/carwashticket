using System.ComponentModel.DataAnnotations;
using CarWashTicket.Api.Entities;

namespace CarWashTicket.Api.Dtos;

public record OrderItemRequest
{
    [Required]
    public Guid ServiceId { get; init; }

    [Range(1, 20)]
    public int Quantity { get; init; } = 1;
}

public record CreateOrderRequest
{
    [Required]
    public Guid StationId { get; init; }

    // "2 su + 1 köpük" iki kalem olarak gelir.
    [Required, MinLength(1)]
    public IReadOnlyList<OrderItemRequest> Items { get; init; } = [];
}

public record CreateOrderResponse(
    Guid OrderId,
    OrderStatus Status,
    decimal Amount,
    string? RedirectUrl);

public record OrderStatusResponse(
    Guid OrderId,
    OrderStatus Status,
    decimal Amount,
    // Sonuç ekranındaki özet: "2 x Su, 1 x Köpük".
    string ItemSummary);
