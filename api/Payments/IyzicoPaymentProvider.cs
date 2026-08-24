using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;

namespace CarWashTicket.Api.Payments;

// Kural 1: "iyzico" adı yalnızca bu dosyada geçer. Yapılandırma anahtarları ve
// DI kaydı bilerek sağlayıcıdan bağımsız isimlendirildi.
public class IyzicoPaymentProvider(
    IConfiguration configuration,
    ILogger<IyzicoPaymentProvider> logger) : IPaymentProvider
{
    private const string SignatureHeader = "x-iyz-signature-v3";

    private Options BuildOptions() => new()
    {
        ApiKey = Required("Payment:Provider:ApiKey"),
        SecretKey = Required("Payment:Provider:SecretKey"),
        BaseUrl = configuration["Payment:Provider:BaseUrl"] ?? "https://sandbox-api.iyzipay.com"
    };

    public async Task<CheckoutResult> StartCheckoutAsync(
        CheckoutRequest request,
        CancellationToken ct = default)
    {
        var price = Format(request.Amount);

        var iyzicoRequest = new CreateCheckoutFormInitializeRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = request.OrderId.ToString(),
            Price = price,
            PaidPrice = price,
            Currency = Currency.TRY.ToString(),
            BasketId = request.OrderId.ToString(),
            PaymentGroup = PaymentGroup.PRODUCT.ToString(),
            CallbackUrl = request.CallbackUrl,
            // 3DS zorunlu: yıkama bileti kart sahibi doğrulaması olmadan satılmıyor.
            ForceThreeDS = 1,
            Buyer = BuildBuyer(request),
            BillingAddress = BuildAddress(request),
            ShippingAddress = BuildAddress(request),
            BasketItems =
            [
                new BasketItem
                {
                    Id = request.OrderId.ToString(),
                    Name = request.Description,
                    Category1 = "Arac Yikama",
                    ItemType = BasketItemType.VIRTUAL.ToString(),
                    Price = price
                }
            ]
        };

        try
        {
            var response = await CheckoutFormInitialize.Create(iyzicoRequest, BuildOptions());

            if (!IsSuccess(response.Status) || string.IsNullOrWhiteSpace(response.PaymentPageUrl))
            {
                logger.LogWarning(
                    "Ödeme başlatılamadı. Sipariş {OrderId}, kod {ErrorCode}: {ErrorMessage}",
                    request.OrderId, response.ErrorCode, response.ErrorMessage);

                return CheckoutResult.Fail(response.ErrorMessage ?? "Ödeme başlatılamadı.");
            }

            // Token callback'te ödemeyi sorgulamak için kullanılacak.
            return CheckoutResult.Ok(response.Token, response.PaymentPageUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ödeme başlatma çağrısı başarısız. Sipariş {OrderId}", request.OrderId);

            return CheckoutResult.Fail("Ödeme sağlayıcısına ulaşılamadı.");
        }
    }

    // Callback'ten gelen veriye güvenilmez; ödeme durumu sağlayıcıdan sorgulanır.
    public async Task<CallbackResult> HandleCallbackAsync(
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct = default)
    {
        if (!data.TryGetValue("token", out var token) || string.IsNullOrWhiteSpace(token))
        {
            return CallbackResult.Fail(Guid.Empty, "Callback'te token yok.");
        }

        try
        {
            var form = await CheckoutForm.Retrieve(
                new RetrieveCheckoutFormRequest
                {
                    Locale = Locale.TR.ToString(),
                    Token = token
                },
                BuildOptions());

            // Sipariş kimliğini callback'ten değil sağlayıcının yanıtından okuyoruz.
            if (!Guid.TryParse(form.BasketId, out var orderId))
            {
                logger.LogWarning("Callback yanıtında sipariş kimliği okunamadı. Token: {Token}", token);

                return CallbackResult.Fail(Guid.Empty, "Sipariş kimliği okunamadı.");
            }

            if (!IsSuccess(form.Status) || !IsSuccess(form.PaymentStatus))
            {
                logger.LogInformation(
                    "Ödeme başarısız. Sipariş {OrderId}, kod {ErrorCode}: {ErrorMessage}",
                    orderId, form.ErrorCode, form.ErrorMessage);

                return CallbackResult.Fail(orderId, form.ErrorMessage ?? "Ödeme tamamlanmadı.");
            }

            if (!TryParseAmount(form.PaidPrice, out var paidAmount))
            {
                return CallbackResult.Fail(orderId, "Ödenen tutar okunamadı.");
            }

            return CallbackResult.Ok(orderId, form.PaymentId, paidAmount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Callback sorgusu başarısız. Token: {Token}", token);

            return CallbackResult.Fail(Guid.Empty, "Ödeme durumu doğrulanamadı.");
        }
    }

    public async Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct = default)
    {
        try
        {
            // Tutar bazlı iade paymentId ile çalışıyor; işlem bazlısı ayrıca
            // paymentTransactionId isterdi ve onu saklamıyoruz.
            var response = await Refund.CreateAmountBasedRefundRequest(
                new CreateAmountBasedRefundRequest
                {
                    Locale = Locale.TR.ToString(),
                    ConversationId = request.ProviderPaymentId,
                    PaymentId = request.ProviderPaymentId,
                    Price = Format(request.Amount),
                    Ip = FallbackIp
                },
                BuildOptions());

            if (!IsSuccess(response.Status))
            {
                logger.LogWarning(
                    "İade reddedildi. Ödeme {PaymentId}, kod {ErrorCode}: {ErrorMessage}",
                    request.ProviderPaymentId, response.ErrorCode, response.ErrorMessage);

                return RefundResult.Fail(response.ErrorMessage ?? "İade reddedildi.");
            }

            return RefundResult.Ok(response.PaymentTransactionId ?? request.ProviderPaymentId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "İade çağrısı başarısız. Ödeme {PaymentId}", request.ProviderPaymentId);

            return RefundResult.Fail("Ödeme sağlayıcısına ulaşılamadı.");
        }
    }

    // DİKKAT: imzalanan metnin biçimi sağlayıcı dokümanından teyit edilmeli.
    // Şu haliyle ham gövdenin HMAC-SHA256'sı karşılaştırılıyor; yanlışsa
    // geçerli webhook'lar da reddedilir (güvenli taraf).
    public bool VerifyWebhook(string payload, IReadOnlyDictionary<string, string> headers)
    {
        var received = headers
            .FirstOrDefault(h => string.Equals(h.Key, SignatureHeader, StringComparison.OrdinalIgnoreCase))
            .Value;

        if (string.IsNullOrWhiteSpace(received))
        {
            logger.LogWarning("Webhook imza başlığı yok.");
            return false;
        }

        var secret = Encoding.UTF8.GetBytes(Required("Payment:Provider:SecretKey"));
        var computed = Convert.ToHexStringLower(
            HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(payload)));

        // Sabit süreli karşılaştırma: imza tahmin saldırısına zamanlama ipucu vermez.
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(received.Trim().ToLowerInvariant()));

        if (!matches)
        {
            logger.LogWarning("Webhook imzası doğrulanamadı.");
        }

        return matches;
    }

    // --- Sözleşmede olmayan alanlar için geçici değerler ---
    // CheckoutRequest ad/soyad/adres/IP taşımıyor. Sandbox bunları kabul ediyor
    // ama canlıya çıkmadan önce sözleşmeye eklenmeli.
    private const string FallbackIp = "127.0.0.1";

    private static Buyer BuildBuyer(CheckoutRequest request) => new()
    {
        Id = request.OrderId.ToString(),
        Name = "Musteri",
        Surname = "Musteri",
        IdentityNumber = "11111111111",
        Email = request.CustomerEmail,
        RegistrationAddress = "Bilinmiyor",
        City = "Istanbul",
        Country = "Turkey",
        Ip = FallbackIp
    };

    private static Address BuildAddress(CheckoutRequest request) => new()
    {
        ContactName = request.CustomerEmail,
        Description = "Bilinmiyor",
        City = "Istanbul",
        Country = "Turkey"
    };

    private static bool IsSuccess(string? status)
        => string.Equals(status, Status.SUCCESS.ToString(), StringComparison.OrdinalIgnoreCase);

    private static string Format(decimal amount)
        => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static bool TryParseAmount(string? raw, out decimal amount)
        => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

    private string Required(string key)
        => configuration[key]
           ?? throw new InvalidOperationException($"{key} tanımlı değil.");
}

public static class RealPaymentProviderRegistration
{
    public static IServiceCollection AddRealPaymentProvider(this IServiceCollection services)
        => services.AddScoped<IPaymentProvider, IyzicoPaymentProvider>();
}
