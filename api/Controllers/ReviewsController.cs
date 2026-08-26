using System.Security.Claims;
using CarWashTicket.Api.Data;
using CarWashTicket.Api.Dtos;
using CarWashTicket.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarWashTicket.Api.Controllers;

[ApiController]
[Route("api/reviews")]
[Produces("application/json")]
[Authorize(Roles = Roles.Customer)]
public class ReviewsController(AppDbContext db, ILogger<ReviewsController> logger) : ControllerBase
{
    // Parası alınmış siparişler değerlendirilebilir.
    private static readonly OrderStatus[] Reviewable =
        [OrderStatus.Paid, OrderStatus.Redeemed, OrderStatus.Settled];

    [HttpGet("pending")]
    [ProducesResponseType<IReadOnlyList<PendingReviewDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PendingReviewDto>>> GetPending(CancellationToken ct)
    {
        var customerId = CurrentUserId();

        var pending = await db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId
                        && Reviewable.Contains(o.Status)
                        // Sipariş başına tek değerlendirme.
                        && !db.Reviews.Any(r => r.OrderId == o.Id))
            .OrderByDescending(o => o.CreatedAt)
            .Take(20)
            .Select(o => new PendingReviewDto(
                o.Id,
                o.StationId,
                o.Station.Name,
                string.Join(", ", o.Items.Select(i => i.Quantity + " x " + i.ServiceName)),
                o.CreatedAt))
            .ToListAsync(ct);

        return Ok(pending);
    }

    [HttpPost]
    [ProducesResponseType<StationRatingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StationRatingDto>> Create(
        CreateReviewRequest request,
        CancellationToken ct)
    {
        var customerId = CurrentUserId();

        // Sahiplik ve durum kontrolü tek sorguda: başkasının siparişi "bulunamaz".
        var order = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == request.OrderId
                        && o.CustomerId == customerId
                        && Reviewable.Contains(o.Status))
            .Select(o => new { o.Id, o.StationId })
            .FirstOrDefaultAsync(ct);

        if (order is null)
        {
            return Problem(
                detail: "Değerlendirilebilecek sipariş bulunamadı.",
                statusCode: StatusCodes.Status404NotFound);
        }

        db.Reviews.Add(new Review
        {
            Id = Guid.NewGuid(),
            StationId = order.StationId,
            OrderId = order.Id,
            CustomerId = customerId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            // Unique index: aynı sipariş iki kez değerlendirilemez.
            return Problem(
                detail: "Bu sipariş zaten değerlendirilmiş.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var rating = await RecalculateAsync(order.StationId, ct);

        logger.LogInformation(
            "Değerlendirme eklendi. İstasyon {StationId}, yeni ortalama {Average} ({Count})",
            order.StationId, rating.Average, rating.Count);

        return Ok(rating);
    }

    [HttpGet("/api/stations/{stationId:guid}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<ReviewDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> GetForStation(
        Guid stationId,
        CancellationToken ct)
    {
        var reviews = await db.Reviews
            .AsNoTracking()
            .Where(r => r.StationId == stationId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new ReviewDto(
                r.Id,
                r.Rating,
                r.Comment,
                // Kişisel veri sızdırmamak için sadece ad ve soyadın baş harfi.
                r.Customer.FullName ?? "Müşteri",
                r.CreatedAt))
            .ToListAsync(ct);

        return Ok(reviews.Select(r => r with { AuthorLabel = Initials(r.AuthorLabel) }).ToList());
    }

    // Ortalama her zaman Reviews tablosundan yeniden hesaplanır; Station'daki
    // değerler sadece listelemeyi hızlandıran bir önbellek.
    private async Task<StationRatingDto> RecalculateAsync(Guid stationId, CancellationToken ct)
    {
        var stats = await db.Reviews
            .Where(r => r.StationId == stationId)
            .GroupBy(_ => 1)
            .Select(g => new { Sum = g.Sum(r => r.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        var count = stats?.Count ?? 0;
        var average = count == 0 ? 0m : Math.Round((decimal)stats!.Sum / count, 2);

        var station = await db.Stations.FirstAsync(s => s.Id == stationId, ct);
        station.RatingAverage = average;
        station.RatingCount = count;

        await db.SaveChangesAsync(ct);

        return new StationRatingDto(average, count);
    }

    private static string Initials(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length < 2
            ? parts.FirstOrDefault() ?? "Müşteri"
            : $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.";
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
