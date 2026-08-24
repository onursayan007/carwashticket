using System.Security.Claims;
using CarWashTicket.Api.Data;
using CarWashTicket.Api.Dtos;
using CarWashTicket.Api.Entities;
using CarWashTicket.Api.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController(AppDbContext db, TicketService ticketService) : ControllerBase
{
    // Müşterinin kendi biletleri, yenisi üstte.
    [HttpGet]
    [Authorize(Roles = Roles.Customer)]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyList<TicketListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketListItemDto>>> GetMine(CancellationToken ct)
    {
        var customerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var tickets = await db.Tickets
            .AsNoTracking()
            .Where(t => t.Order.CustomerId == customerId)
            .OrderByDescending(t => t.IssuedAt)
            .Select(t => new TicketListItemDto(
                t.Id,
                t.Code,
                t.Status,
                t.ServiceName,
                t.Station.Name,
                t.Order.Amount,
                t.IssuedAt,
                t.ExpiresAt,
                t.RedeemedAt))
            .ToListAsync(ct);

        return Ok(tickets);
    }

    [HttpPost("redeem")]
    [Authorize(Roles = Roles.Scanner)]
    [Produces("application/json")]
    [ProducesResponseType<RedeemTicketResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RedeemTicketResponse>> Redeem(
        RedeemTicketRequest request,
        CancellationToken ct)
    {
        var staffId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await ticketService.RedeemAsync(request.Code.Trim(), staffId, ct);

        if (result.Outcome != RedeemOutcome.Redeemed)
        {
            // Geçersiz kod, süresi dolmuş bilet, başka istasyonun bileti ve zaten
            // kullanılmış bilet aynı yanıtı alır; ayrıntı vermek bilgi sızdırır.
            return Ok(new RedeemTicketResponse(
                false, "Bilet zaten kullanılmış veya geçersiz.", null, null));
        }

        return Ok(new RedeemTicketResponse(
            true, "Bilet kullanıldı.", result.ServiceName, result.Ticket!.RedeemedAt));
    }

    // QR görseli sadece bileti satın alan müşteriye verilir.
    [HttpGet("{id:guid}/qr")]
    [Authorize(Roles = Roles.Customer)]
    [Produces("image/png")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQr(Guid id, CancellationToken ct)
    {
        var customerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var code = await db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == id && t.Order.CustomerId == customerId)
            .Select(t => t.Code)
            .FirstOrDefaultAsync(ct);

        if (code is null)
        {
            return NotFound();
        }

        return File(TicketService.CreateQrPng(code), "image/png");
    }
}
