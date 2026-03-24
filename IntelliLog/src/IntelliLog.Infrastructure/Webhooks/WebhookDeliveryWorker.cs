using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using IntelliLog.Domain.Entities;
using IntelliLog.Domain.Enums;
using IntelliLog.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntelliLog.Infrastructure.Webhooks;

/// <summary>
/// Background worker that reads queued webhook events and
/// delivers them to subscribers with HMAC-SHA256 signed payloads.
/// </summary>
public class WebhookDeliveryWorker : BackgroundService
{
    private readonly WebhookService _webhookService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryWorker> _logger;

    public WebhookDeliveryWorker(
        WebhookService webhookService,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDeliveryWorker> logger)
    {
        _webhookService = webhookService;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook delivery worker started.");

        await foreach (var msg in _webhookService.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await DeliverAsync(msg, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error delivering webhook for event {EventType}", msg.EventType);
            }
        }
    }

    private async Task DeliverAsync(WebhookMessage msg, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find all active subscribers for this event type
        var subscribers = await db.WebhookSubscriptions
            .Where(s => s.IsActive && s.EventType == msg.EventType)
            .ToListAsync(ct);

        if (subscribers.Count == 0)
        {
            _logger.LogDebug("No subscribers for {EventType}", msg.EventType);
            return;
        }

        var client = _httpClientFactory.CreateClient("WebhookClient");
        client.Timeout = TimeSpan.FromSeconds(10);

        foreach (var sub in subscribers)
        {
            var delivery = new WebhookDelivery
            {
                SubscriptionId = sub.Id,
                EventType = msg.EventType,
                Payload = msg.PayloadJson,
                Status = DeliveryStatus.Pending,
                AttemptCount = 0
            };

            db.WebhookDeliveries.Add(delivery);
            await db.SaveChangesAsync(ct);

            // Deliver with retry (max 3 attempts)
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                delivery.AttemptCount = attempt;

                try
                {
                    // Sign the payload with HMAC-SHA256
                    var signature = ComputeHmacSha256(msg.PayloadJson, sub.Secret);

                    var request = new HttpRequestMessage(HttpMethod.Post, sub.Url)
                    {
                        Content = new StringContent(msg.PayloadJson, Encoding.UTF8, "application/json")
                    };
                    request.Headers.Add("X-Webhook-Event", msg.EventType.ToString());
                    request.Headers.Add("X-Webhook-Signature", $"sha256={signature}");
                    request.Headers.Add("X-Webhook-Delivery", delivery.Id.ToString());

                    var response = await client.SendAsync(request, ct);
                    delivery.HttpStatusCode = (int)response.StatusCode;

                    if (response.IsSuccessStatusCode)
                    {
                        delivery.Status = DeliveryStatus.Delivered;
                        _logger.LogInformation("Webhook delivered to {Url} for {EventType}", sub.Url, msg.EventType);
                        break;
                    }

                    delivery.ErrorMessage = $"HTTP {(int)response.StatusCode}";
                    delivery.Status = attempt < 3 ? DeliveryStatus.Retrying : DeliveryStatus.Failed;
                }
                catch (Exception ex)
                {
                    delivery.ErrorMessage = ex.Message;
                    delivery.Status = attempt < 3 ? DeliveryStatus.Retrying : DeliveryStatus.Failed;
                    _logger.LogWarning(ex, "Attempt {Attempt} failed for webhook {SubId}", attempt, sub.Id);
                }

                await db.SaveChangesAsync(ct);

                if (delivery.Status == DeliveryStatus.Retrying)
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }
        }
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
