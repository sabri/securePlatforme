using IntelliLog.Domain.Enums;

namespace IntelliLog.Domain.Entities;

public class WebhookDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public WebhookEventType EventType { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }

    public WebhookSubscription Subscription { get; set; } = null!;
}
