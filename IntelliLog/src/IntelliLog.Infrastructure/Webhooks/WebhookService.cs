using System.Text.Json;
using System.Threading.Channels;
using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Domain.Entities;
using IntelliLog.Domain.Enums;

namespace IntelliLog.Infrastructure.Webhooks;

public record WebhookMessage(WebhookEventType EventType, string PayloadJson);

public class WebhookService : IWebhookService
{
    private readonly Channel<WebhookMessage> _channel;

    public ChannelReader<WebhookMessage> Reader => _channel.Reader;

    public WebhookService()
    {
        _channel = Channel.CreateBounded<WebhookMessage>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public async Task QueueEventAsync(WebhookEventType eventType, object payload, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _channel.Writer.WriteAsync(new WebhookMessage(eventType, json), ct);
    }
}
