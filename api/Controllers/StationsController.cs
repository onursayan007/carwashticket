using CarWashTicket.Api.Data;
using CarWashTicket.Api.Dtos;
using CarWashTicket.Api.Stations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Controllers;

[ApiController]
[Route("api/stations")]
[Produces("application/json")]
[Authorize]
public class StationsController(AppDbContext db, StationQueryService stations) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<StationSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StationSummaryDto>>> Search(
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        [FromQuery] StationSort sort = StationSort.Best,
        [FromQuery] double radiusKm = 50,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var results = await stations.SearchAsync(
            lat,
            lng,
            sort,
            Math.Clamp(radiusKm, 1, 500),
            Math.Clamp(limit, 1, 200),
            ct);

        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<StationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StationDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var station = await db.Stations
            .AsNoTracking()
            .Where(s => s.Id == id && s.IsActive)
            .Select(s => new StationDetailDto(
                s.Id,
                s.Name,
                s.Type,
                s.Address,
                s.City,
                s.District,
                s.Latitude,
                s.Longitude,
                s.PhoneNumber,
                s.RatingAverage,
                s.RatingCount,
                s.Services
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Price)
                    .Select(x => new ServiceDto(
                        x.Id,
                        x.Name,
                        x.Description,
                        x.Kind,
                        x.Price,
                        x.DurationMinutes))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        if (station is null)
        {
            return NotFound();
        }

        return Ok(station);
    }
}
