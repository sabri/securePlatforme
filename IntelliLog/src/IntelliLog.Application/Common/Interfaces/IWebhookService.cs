using IntelliLog.Domain.Enums;

namespace IntelliLog.Application.Common.Interfaces;

/// <summary>
/// Webhook delivery engine: queues + delivers webhook payloads
/// to registered subscribers with HMAC-SHA256 signatures.
/// </summary>
public interface IWebhookService
{
    Task QueueEventAsync(WebhookEventType eventType, object payload, CancellationToken ct = default);
}
