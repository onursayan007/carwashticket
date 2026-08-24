using System.ComponentModel.DataAnnotations;
using CarWashTicket.Api.Entities;

namespace CarWashTicket.Api.Dtos;

public record PanelSummaryDto(
    Guid StationId,
    DateTimeOffset From,
    DateTimeOffset To,
    int OrderCount,
    // Defterden hesaplanır; iadeler netleşmiş halde gelir.
    decimal GrossRevenue,
    decimal Commission,
    decimal StationShare);

public record PanelOrderDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    string ServiceName,
    decimal Amount,
    decimal CommissionAmount,
    OrderStatus Status);

public record PanelServiceDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int DurationMinutes,
    bool IsActive);

public record UpsertServiceRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = null!;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Range(0.01, 1_000_000)]
    public decimal Price { get; init; }

    [Range(1, 1440)]
    public int DurationMinutes { get; init; }

    public bool IsActive { get; init; } = true;
}
