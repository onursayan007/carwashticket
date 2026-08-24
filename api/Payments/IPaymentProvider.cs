namespace CarWashTicket.Api.Payments;

// Ödeme sağlayıcısı bu arayüzün arkasında kalır; üst katmanlar hangi sağlayıcı
// olduğunu bilmez.
public interface IPaymentProvider
{
    // Ödemeyi başlatır ve kullanıcının yönlendirileceği adresi döner.
    Task<CheckoutResult> StartCheckoutAsync(CheckoutRequest request, CancellationToken ct = default);

    // Kullanıcı ödeme sonrası geri döndüğünde gelen form/query verisini yorumlar.
    Task<CallbackResult> HandleCallbackAsync(
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct = default);

    Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct = default);

    // Webhook gövdesinin gerçekten sağlayıcıdan geldiğini doğrular.
    bool VerifyWebhook(string payload, IReadOnlyDictionary<string, string> headers);
}
