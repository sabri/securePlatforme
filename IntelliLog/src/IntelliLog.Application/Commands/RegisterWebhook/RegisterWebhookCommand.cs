using MediatR;

namespace IntelliLog.Application.Commands.RegisterWebhook;

public record RegisterWebhookCommand(
    string Name,
    string Url,
    string EventType
) : IRequest<RegisterWebhookResult>;

public record RegisterWebhookResult(Guid SubscriptionId, string Secret, string EventType);
