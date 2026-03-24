using MediatR;

namespace IntelliLog.Application.Queries.GetWebhooks;

public record GetWebhooksQuery : IRequest<GetWebhooksResult>;

public record GetWebhooksResult(List<WebhookDto> Subscriptions);

public record WebhookDto(
    Guid Id,
    string Name,
    string Url,
    string EventType,
    bool IsActive,
    int DeliveryCount
);
