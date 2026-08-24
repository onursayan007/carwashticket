namespace CarWashTicket.Api.Payments;

public record CheckoutRequest(
    Guid OrderId,
    decimal Amount,
    string Description,
    string CustomerEmail,
    // Sağlayıcının ödeme sonrası kullanıcıyı geri göndereceği adres.
    string CallbackUrl);

public record CheckoutResult(
    bool Success,
    string? ProviderPaymentId,
    string? RedirectUrl,
    string? ErrorMessage)
{
    public static CheckoutResult Ok(string providerPaymentId, string redirectUrl)
        => new(true, providerPaymentId, redirectUrl, null);

    public static CheckoutResult Fail(string errorMessage)
        => new(false, null, null, errorMessage);
}

public record CallbackResult(
    bool Success,
    Guid OrderId,
    string? ProviderPaymentId,
    decimal? PaidAmount,
    string? ErrorMessage)
{
    public static CallbackResult Ok(Guid orderId, string providerPaymentId, decimal paidAmount)
        => new(true, orderId, providerPaymentId, paidAmount, null);

    public static CallbackResult Fail(Guid orderId, string errorMessage)
        => new(false, orderId, null, null, errorMessage);
}

public record RefundRequest(
    string ProviderPaymentId,
    decimal Amount,
    string? Reason);

public record RefundResult(
    bool Success,
    string? ProviderRefundId,
    string? ErrorMessage)
{
    public static RefundResult Ok(string providerRefundId) => new(true, providerRefundId, null);

    public static RefundResult Fail(string errorMessage) => new(false, null, errorMessage);
}
