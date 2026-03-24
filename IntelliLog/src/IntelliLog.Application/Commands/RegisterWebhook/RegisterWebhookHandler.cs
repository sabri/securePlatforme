using System.Security.Cryptography;
using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Domain.Entities;
using IntelliLog.Domain.Enums;
using MediatR;

namespace IntelliLog.Application.Commands.RegisterWebhook;

public class RegisterWebhookHandler : IRequestHandler<RegisterWebhookCommand, RegisterWebhookResult>
{
    private readonly IAppDbContext _db;

    public RegisterWebhookHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<RegisterWebhookResult> Handle(RegisterWebhookCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<WebhookEventType>(request.EventType, true, out var eventType))
            throw new ArgumentException($"Invalid event type: {request.EventType}");

        // Generate a secure HMAC secret for this subscription
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Convert.ToBase64String(secretBytes);

        var subscription = new WebhookSubscription
        {
            Name = request.Name,
            Url = request.Url,
            Secret = secret,
            EventType = eventType,
            IsActive = true
        };

        _db.WebhookSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);

        return new RegisterWebhookResult(subscription.Id, secret, eventType.ToString());
    }
}
