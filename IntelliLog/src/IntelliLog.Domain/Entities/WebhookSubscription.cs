using IntelliLog.Domain.Enums;

namespace IntelliLog.Domain.Entities;

public class WebhookSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public WebhookEventType EventType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<WebhookDelivery> Deliveries { get; set; } = new();
}
