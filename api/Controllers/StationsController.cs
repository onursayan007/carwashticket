using CarWashTicket.Api.Data;
using CarWashTicket.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Controllers;

[ApiController]
[Route("api/stations")]
[Produces("application/json")]
[Authorize]
public class StationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<StationListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StationListItemDto>>> GetAll()
    {
        var stations = await db.Stations
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new StationListItemDto(s.Id, s.Name, s.Address, s.PhoneNumber))
            .ToListAsync();

        return Ok(stations);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<StationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StationDetailDto>> GetById(Guid id)
    {
        var station = await db.Stations
            .AsNoTracking()
            .Where(s => s.Id == id && s.IsActive)
            .Select(s => new StationDetailDto(
                s.Id,
                s.Name,
                s.Address,
                s.PhoneNumber,
                s.Services
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Price)
                    .Select(x => new ServiceDto(
                        x.Id,
                        x.Name,
                        x.Description,
                        x.Price,
                        x.DurationMinutes))
                    .ToList()))
            .FirstOrDefaultAsync();

        if (station is null)
        {
            return NotFound();
        }

        return Ok(station);
    }
}
