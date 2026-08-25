namespace CarWashTicket.Api.Notifications;

public record NotificationMessage(
    string? Email,
    string? Phone,
    string Subject,
    string Body);

// Ödeme sağlayıcısıyla aynı desen: gerçek servis (SMTP, SMS) arkada kalır.
public interface INotificationSender
{
    Task SendAsync(NotificationMessage message, CancellationToken ct = default);
}
