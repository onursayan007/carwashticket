using CarWashTicket.Api.Data;
using CarWashTicket.Api.Entities;
using CarWashTicket.Api.Orders;
using CarWashTicket.Api.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarWashTicket.Api.Controllers;

[ApiController]
[Route("api/payments")]
[AllowAnonymous]
public class PaymentsController(
    AppDbContext db,
    IPaymentProvider paymentProvider,
    OrderService orderService,
    IConfiguration configuration,
    ILogger<PaymentsController> logger) : ControllerBase
{
    // Kullanıcı 3DS ekranından döner. Burada sipariş KESİNLEŞTİRİLMEZ; ödemenin
    // gerçekten geçtiğini webhook söyler. Bu uç sadece kullanıcıyı SPA'ya yollar.
    [HttpGet("callback")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Callback(CancellationToken ct)
    {
        var data = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());

        var result = await paymentProvider.HandleCallbackAsync(data, ct);

        logger.LogInformation(
            "Ödeme dönüşü. Sipariş {OrderId}, sağlayıcı sonucu: {Success}",
            result.OrderId, result.Success);

        // "pending": ödeme başarılı görünüyor ama bileti webhook kesinleştirecek.
        return Redirect(QueryHelpers.AddQueryString(
            $"{SpaBaseUrl}/odeme/sonuc",
            new Dictionary<string, string?>
            {
                ["orderId"] = result.OrderId == Guid.Empty ? null : result.OrderId.ToString(),
                ["status"] = result.Success ? "pending" : "failed"
            }));
    }

    [HttpPost("webhook")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        if (!paymentProvider.VerifyWebhook(payload, headers))
        {
            logger.LogWarning("Webhook imzası doğrulanamadı, istek reddedildi.");
            return Unauthorized();
        }

        var notification = paymentProvider.ParseWebhook(payload);

        if (notification is null)
        {
            return BadRequest();
        }

        var webhookEvent = new WebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = paymentProvider.GetType().Name,
            ProviderEventId = notification.ProviderEventId,
            EventType = notification.EventType,
            Payload = payload,
            Status = WebhookStatus.Received,
            OrderId = notification.OrderId,
            ReceivedAt = DateTimeOffset.UtcNow
        };

        db.WebhookEvents.Add(webhookEvent);

        try
        {
            // Önce kaydı yaz: unique index sayesinde aynı olay ikinci kez işlenmez.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            logger.LogInformation(
                "Webhook zaten işlenmiş, atlandı. Olay: {EventId}", notification.ProviderEventId);

            // 200 dönüyoruz ki sağlayıcı tekrar denemesin.
            return Ok();
        }

        await ProcessAsync(webhookEvent, notification, ct);

        return Ok();
    }

    private async Task ProcessAsync(
        WebhookEvent webhookEvent,
        WebhookNotification notification,
        CancellationToken ct)
    {
        try
        {
            switch (notification.Status)
            {
                case WebhookPaymentStatus.Succeeded:
                    var outcome = await orderService.ConfirmPaymentAsync(
                        notification.OrderId,
                        notification.ProviderPaymentId,
                        notification.PaidAmount,
                        ct);

                    webhookEvent.Status = outcome is PaymentConfirmationOutcome.Confirmed
                        or PaymentConfirmationOutcome.AlreadyConfirmed
                        ? WebhookStatus.Processed
                        : WebhookStatus.Failed;

                    webhookEvent.Error = webhookEvent.Status == WebhookStatus.Failed
                        ? outcome.ToString()
                        : null;
                    break;

                case WebhookPaymentStatus.Failed:
                    await orderService.MarkPaymentFailedAsync(
                        notification.OrderId, "Sağlayıcı ödemeyi başarısız bildirdi.", ct);

                    webhookEvent.Status = WebhookStatus.Processed;
                    break;

                default:
                    webhookEvent.Status = WebhookStatus.Processed;
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook işlenemedi. Olay: {EventId}", notification.ProviderEventId);

            webhookEvent.Status = WebhookStatus.Failed;
            webhookEvent.Error = ex.Message;
        }

        webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    private string SpaBaseUrl => configuration["Spa:BaseUrl"]?.TrimEnd('/')
        ?? throw new InvalidOperationException("Spa:BaseUrl tanımlı değil.");

    private static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
