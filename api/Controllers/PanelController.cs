using System.Security.Claims;
using CarWashTicket.Api.Data;
using CarWashTicket.Api.Dtos;
using CarWashTicket.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Controllers;

[ApiController]
[Route("api/panel")]
[Produces("application/json")]
[Authorize(Roles = "Manager")]
public class PanelController(AppDbContext db) : ControllerBase
{
    // Listede en fazla bu kadar sipariş döner; sayfalama gerekirse ayrıca eklenir.
    private const int MaxOrders = 500;

    // Ciroya sayılan durumlar. İadeler defterde ters kayıtla netleşiyor.
    private static readonly OrderStatus[] RevenueStatuses =
        [OrderStatus.Paid, OrderStatus.Redeemed, OrderStatus.Settled];

    [HttpGet("summary")]
    [ProducesResponseType<PanelSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PanelSummaryDto>> GetSummary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        var station = await ResolveStationAsync(stationId, ct);

        if (station is null)
        {
            return StationProblem();
        }

        var (start, end) = NormalizeRange(from, to);

        var orderCount = await db.Orders
            .AsNoTracking()
            .CountAsync(
                o => o.StationId == station.Value
                     && o.CreatedAt >= start
                     && o.CreatedAt < end
                     && RevenueStatuses.Contains(o.Status),
                ct);

        // Defter tek kaynak: her hesabın Credit - Debit farkı alınıyor.
        var totals = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.StationId == station.Value && e.CreatedAt >= start && e.CreatedAt < end)
            .GroupBy(e => e.Account)
            .Select(g => new
            {
                Account = g.Key,
                Net = g.Sum(e => e.Direction == LedgerDirection.Credit ? e.Amount : -e.Amount)
            })
            .ToListAsync(ct);

        decimal NetOf(LedgerAccount account)
            => totals.FirstOrDefault(t => t.Account == account)?.Net ?? 0m;

        return Ok(new PanelSummaryDto(
            station.Value,
            start,
            end,
            orderCount,
            // PaymentGateway borç bakiyeli; işaretini çevirerek tahsilatı buluyoruz.
            -NetOf(LedgerAccount.PaymentGateway),
            NetOf(LedgerAccount.PlatformCommission),
            NetOf(LedgerAccount.StationRevenue)));
    }

    [HttpGet("orders")]
    [ProducesResponseType<IReadOnlyList<PanelOrderDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PanelOrderDto>>> GetOrders(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        var station = await ResolveStationAsync(stationId, ct);

        if (station is null)
        {
            return StationProblem();
        }

        var (start, end) = NormalizeRange(from, to);

        var orders = await db.Orders
            .AsNoTracking()
            .Where(o => o.StationId == station.Value && o.CreatedAt >= start && o.CreatedAt < end)
            .OrderByDescending(o => o.CreatedAt)
            .Take(MaxOrders)
            .Select(o => new PanelOrderDto(
                o.Id,
                o.CreatedAt,
                o.Service.Name,
                o.Amount,
                o.CommissionAmount,
                o.Status))
            .ToListAsync(ct);

        return Ok(orders);
    }

    [HttpGet("services")]
    [ProducesResponseType<IReadOnlyList<PanelServiceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PanelServiceDto>>> GetServices(
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        var station = await ResolveStationAsync(stationId, ct);

        if (station is null)
        {
            return StationProblem();
        }

        var services = await db.Services
            .AsNoTracking()
            .Where(s => s.StationId == station.Value)
            .OrderBy(s => s.Price)
            .Select(s => new PanelServiceDto(
                s.Id, s.Name, s.Description, s.Price, s.DurationMinutes, s.IsActive))
            .ToListAsync(ct);

        return Ok(services);
    }

    [HttpPost("services")]
    [ProducesResponseType<PanelServiceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PanelServiceDto>> CreateService(
        UpsertServiceRequest request,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        var station = await ResolveStationAsync(stationId, ct);

        if (station is null)
        {
            return StationProblem();
        }

        var service = new Service
        {
            Id = Guid.NewGuid(),
            StationId = station.Value,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            DurationMinutes = request.DurationMinutes,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Services.Add(service);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(
            nameof(GetServices),
            new PanelServiceDto(
                service.Id, service.Name, service.Description, service.Price,
                service.DurationMinutes, service.IsActive));
    }

    [HttpPut("services/{id:guid}")]
    [ProducesResponseType<PanelServiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PanelServiceDto>> UpdateService(
        Guid id,
        UpsertServiceRequest request,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        var station = await ResolveStationAsync(stationId, ct);

        if (station is null)
        {
            return StationProblem();
        }

        // İstasyon filtresi sorgunun içinde: başka istasyonun hizmeti bulunamaz.
        var service = await db.Services
            .FirstOrDefaultAsync(s => s.Id == id && s.StationId == station.Value, ct);

        if (service is null)
        {
            return NotFound();
        }

        service.Name = request.Name.Trim();
        service.Description = request.Description?.Trim();
        service.Price = request.Price;
        service.DurationMinutes = request.DurationMinutes;
        service.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);

        return Ok(new PanelServiceDto(
            service.Id, service.Name, service.Description, service.Price,
            service.DurationMinutes, service.IsActive));
    }

    // Tek yetki kapısı: her uç buradan geçer. İstenen istasyon verilmişse
    // yöneticinin gerçekten o istasyonda görevli olduğu doğrulanır.
    private async Task<Guid?> ResolveStationAsync(Guid? requested, CancellationToken ct)
    {
        var managerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var stations = await db.StationStaff
            .AsNoTracking()
            .Where(ss => ss.UserId == managerId && ss.Role == StationRole.Manager)
            .Select(ss => ss.StationId)
            .ToListAsync(ct);

        if (requested.HasValue)
        {
            return stations.Contains(requested.Value) ? requested.Value : null;
        }

        // Tek istasyonu varsa parametre istemiyoruz; birden fazlaysa seçmesi gerekir.
        return stations.Count == 1 ? stations[0] : null;
    }

    private ObjectResult StationProblem() => Problem(
        detail: "Bu istasyona erişiminiz yok veya hangi istasyon olduğu belirtilmedi.",
        statusCode: StatusCodes.Status403Forbidden);

    private static (DateTimeOffset Start, DateTimeOffset End) NormalizeRange(
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var end = to ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-30);

        return start <= end ? (start, end) : (end, start);
    }
}
