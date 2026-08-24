using System.Globalization;
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
}
