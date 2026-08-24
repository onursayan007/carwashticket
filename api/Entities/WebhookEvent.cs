namespace CarWashTicket.Api.Entities;

// Sağlayıcıdan gelen webhook. Aynı olay tekrar gelirse unique index insert'i engeller.
public class WebhookEvent
{
    public Guid Id { get; set; }

    public string Provider { get; set; } = null!;

    public string ProviderEventId { get; set; } = null!;

    public string EventType { get; set; } = null!;

    // Gelen gövdenin ham hali.
    public string Payload { get; set; } = null!;

    public WebhookStatus Status { get; set; }

    public Guid? OrderId { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string? Error { get; set; }

    public Order? Order { get; set; }
}
