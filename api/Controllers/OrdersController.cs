using System.Security.Claims;
using CarWashTicket.Api.Dtos;
using CarWashTicket.Api.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarWashTicket.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
[Authorize(Roles = "Customer")]
public class OrdersController(OrderService orderService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CreateOrderResponse>> Create(
        CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(
                detail: "Idempotency-Key başlığı zorunludur.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var customerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var customerEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        var result = await orderService.CreateAsync(
            customerId,
            customerEmail,
            request,
            idempotencyKey.Trim(),
            ct);

        return result.Outcome switch
        {
            // Tekrar istek de aynı gövdeyi 200 ile döner; çağıran fark görmez.
            OrderCreationOutcome.Created or OrderCreationOutcome.Replayed => Ok(result.Response),

            OrderCreationOutcome.ServiceNotFound => Problem(
                detail: result.Error, statusCode: StatusCodes.Status404NotFound),

            OrderCreationOutcome.KeyBelongsToAnotherCustomer => Problem(
                detail: result.Error, statusCode: StatusCodes.Status409Conflict),

            OrderCreationOutcome.PaymentFailed => Problem(
                detail: result.Error, statusCode: StatusCodes.Status502BadGateway),

            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
