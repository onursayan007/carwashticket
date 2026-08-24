using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace CarWashTicket.Api.Payments;

// Dışarıya hiçbir çağrı yapmaz. Sonuç, Payment:Mock:Outcome ayarından gelir;
// istek bazında ezmek için CheckoutRequest.Description içine "fail" yazmak yeterli.
public class MockPaymentProvider(IConfiguration configuration, ILogger<MockPaymentProvider> logger)
    : IPaymentProvider
{
    private bool FailByDefault =>
        string.Equals(configuration["Payment:Mock:Outcome"], "Failure", StringComparison.OrdinalIgnoreCase);

    public Task<CheckoutResult> StartCheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        var shouldFail = FailByDefault
            || request.Description.Contains("fail", StringComparison.OrdinalIgnoreCase);

        if (shouldFail)
        {
            logger.LogInformation("Mock ödeme başlatma reddedildi. Sipariş: {OrderId}", request.OrderId);

            return Task.FromResult(CheckoutResult.Fail("Ödeme başlatılamadı (mock)."));
        }

        var paymentId = $"mock_{Guid.NewGuid():N}";

        // Geliştirici bu adresi tarayıcıda açıp callback akışını tetikleyebilir.
        var redirectUrl = QueryHelpers.AddQueryString(request.CallbackUrl, new Dictionary<string, string?>
        {
            ["orderId"] = request.OrderId.ToString(),
            ["paymentId"] = paymentId,
            ["amount"] = request.Amount.ToString(CultureInfo.InvariantCulture),
            ["status"] = "success"
        });

        logger.LogInformation("Mock ödeme başlatıldı. Sipariş: {OrderId}", request.OrderId);

        return Task.FromResult(CheckoutResult.Ok(paymentId, redirectUrl));
    }

    public Task<CallbackResult> HandleCallbackAsync(
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct = default)
    {
        if (!data.TryGetValue("orderId", out var rawOrderId)
            || !Guid.TryParse(rawOrderId, out var orderId))
        {
            return Task.FromResult(CallbackResult.Fail(Guid.Empty, "orderId okunamadı."));
        }

        var succeeded = data.TryGetValue("status", out var status)
            && string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

        if (!succeeded)
        {
            return Task.FromResult(CallbackResult.Fail(orderId, "Ödeme başarısız (mock)."));
        }

        data.TryGetValue("paymentId", out var paymentId);

        var amount = data.TryGetValue("amount", out var rawAmount)
            && decimal.TryParse(rawAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0m;

        return Task.FromResult(
            CallbackResult.Ok(orderId, paymentId ?? $"mock_{orderId:N}", amount));
    }

    public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct = default)
    {
        if (FailByDefault)
        {
            return Task.FromResult(RefundResult.Fail("İade reddedildi (mock)."));
        }

        logger.LogInformation(
            "Mock iade. Ödeme: {PaymentId}, Tutar: {Amount}",
            request.ProviderPaymentId,
            request.Amount);

        return Task.FromResult(RefundResult.Ok($"mock_refund_{Guid.NewGuid():N}"));
    }

    // Mock'ta imza yok; gerçek sağlayıcıda burası imzayı doğrular.
    public bool VerifyWebhook(string payload, IReadOnlyDictionary<string, string> headers) => true;

    // Mock webhook gövdesi:
    // { "eventId": "...", "eventType": "...", "orderId": "...", "paymentId": "...",
    //   "amount": "250.00", "status": "success" }
    public WebhookNotification? ParseWebhook(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (!TryGetString(root, "orderId", out var rawOrderId)
                || !Guid.TryParse(rawOrderId, out var orderId))
            {
                return null;
            }

            TryGetString(root, "eventId", out var eventId);
            TryGetString(root, "eventType", out var eventType);
            TryGetString(root, "paymentId", out var paymentId);
            TryGetString(root, "status", out var status);

            var amount = TryGetString(root, "amount", out var rawAmount)
                && decimal.TryParse(rawAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : (decimal?)null;

            return new WebhookNotification(
                string.IsNullOrWhiteSpace(eventId) ? $"mock_evt_{orderId:N}" : eventId,
                string.IsNullOrWhiteSpace(eventType) ? "payment" : eventType,
                orderId,
                paymentId,
                amount,
                string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
                    ? WebhookPaymentStatus.Succeeded
                    : WebhookPaymentStatus.Failed);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Mock webhook gövdesi okunamadı.");
            return null;
        }
    }

    private static bool TryGetString(JsonElement root, string name, out string? value)
    {
        value = root.TryGetProperty(name, out var element) ? element.ToString() : null;

        return !string.IsNullOrWhiteSpace(value);
    }
}
