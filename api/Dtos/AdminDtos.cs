using System.ComponentModel.DataAnnotations;
using CarWashTicket.Api.Entities;

namespace CarWashTicket.Api.Dtos;

public record CreateBusinessRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = null!;

    public StationType Type { get; init; }

    [MaxLength(300)]
    public string? CompanyName { get; init; }

    [MaxLength(20)]
    public string? TaxNumber { get; init; }

    [MaxLength(150)]
    public string? TaxOffice { get; init; }

    [MaxLength(500)]
    public string? Address { get; init; }

    [MaxLength(100)]
    public string? City { get; init; }

    [MaxLength(100)]
    public string? District { get; init; }

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    // Geçici şifre buraya gider, bu yüzden zorunlu.
    [Required, EmailAddress, MaxLength(256)]
    public string ContactEmail { get; init; } = null!;

    [MaxLength(30)]
    public string? PhoneNumber { get; init; }
}

public record BusinessSummaryDto(
    Guid Id,
    string Name,
    StationType Type,
    string? CompanyName,
    string? City,
    string? District,
    double Latitude,
    double Longitude,
    string? ContactEmail,
    bool IsActive,
    int ServiceCount,
    DateTimeOffset CreatedAt);

// Geçici şifre yanıtta DÖNMEZ; sadece bildirim kanalıyla gider.
public record CreateBusinessResponse(
    Guid StationId,
    string OwnerEmail,
    string Message);
