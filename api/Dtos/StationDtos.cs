namespace CarWashTicket.Api.Dtos;

public record StationListItemDto(
    Guid Id,
    string Name,
    string? Address,
    string? PhoneNumber);

public record ServiceDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int DurationMinutes);

public record StationDetailDto(
    Guid Id,
    string Name,
    string? Address,
    string? PhoneNumber,
    IReadOnlyList<ServiceDto> Services);
