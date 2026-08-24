namespace CarWashTicket.Api.Payments;

public static class PaymentRegistration
{
    // Varsayılan: Development'ta mock, diğer ortamlarda gerçek sağlayıcı.
    // Payment:UseMock ayarı bu varsayılanı ezer.
    public static IServiceCollection AddPaymentProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var useMock = configuration.GetValue("Payment:UseMock", environment.IsDevelopment());

        if (useMock)
        {
            services.AddScoped<IPaymentProvider, MockPaymentProvider>();
            return services;
        }

        // Gerçek sağlayıcı henüz yazılmadı. Sessizce mock'a düşmek yerine
        // ayağa kalkarken patlaması daha güvenli.
        throw new InvalidOperationException(
            "Gerçek ödeme sağlayıcısı henüz kayıtlı değil. "
            + "Geliştirme için Payment:UseMock = true yapın.");
    }
}
