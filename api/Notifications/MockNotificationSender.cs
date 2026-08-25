namespace CarWashTicket.Api.Notifications;

// Dışarıya hiçbir şey göndermez, log'a yazar. Geliştirmede geçici şifreyi
// buradan okuyorsun. Gerçek sağlayıcı (SMTP / SMS) sonra bu arayüzün arkasına gelir.
public class MockNotificationSender(ILogger<MockNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        logger.LogWarning(
            "BİLDİRİM (mock) -> e-posta: {Email}, telefon: {Phone}\nKonu: {Subject}\n{Body}",
            message.Email ?? "-",
            message.Phone ?? "-",
            message.Subject,
            message.Body);

        return Task.CompletedTask;
    }
}
