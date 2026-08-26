using System.ComponentModel.DataAnnotations;

namespace CarWashTicket.Api.Dtos;

public record CreateReviewRequest
{
    [Required]
    public Guid OrderId { get; init; }

    [Range(1, 5)]
    public int Rating { get; init; }

    [MaxLength(1000)]
    public string? Comment { get; init; }
}

// Müşterinin henüz değerlendirmediği, değerlendirmeye uygun siparişler.
public record PendingReviewDto(
    Guid OrderId,
    Guid StationId,
    string StationName,
    string ItemSummary,
    DateTimeOffset CreatedAt);

public record ReviewDto(
    Guid Id,
    int Rating,
    string? Comment,
    // Tam ad değil baş harfler: "Onur S." gibi.
    string AuthorLabel,
    DateTimeOffset CreatedAt);

public record StationRatingDto(decimal Average, int Count);
