using CarWashTicket.Api.Entities;

namespace CarWashTicket.Api.Dtos;

public enum StationSort
{
    Nearest,
    Cheapest,
    TopRated,
    Best
}

public record StationSummaryDto(
    Guid Id,
    string Name,
    StationType Type,
    string? Address,
    string? City,
    string? District,
    double Latitude,
    double Longitude,
    decimal RatingAverage,
    int RatingCount,
    // Aktif hizmetlerin en düşük fiyatı. Hiç hizmeti yoksa null.
    decimal? MinPrice,
    // Konum verilmediyse null.
    double? DistanceKm);

public record ServiceDto(
    Guid Id,
    string Name,
    string? Description,
    ServiceKind Kind,
    decimal Price,
    int DurationMinutes);

public record StationDetailDto(
    Guid Id,
    string Name,
    StationType Type,
    string? Address,
    string? City,
    string? District,
    double Latitude,
    double Longitude,
    string? PhoneNumber,
    decimal RatingAverage,
    int RatingCount,
    IReadOnlyList<ServiceDto> Services);
