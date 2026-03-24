using IntelliLog.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IntelliLog.Application.Queries.GetWebhooks;

public class GetWebhooksHandler : IRequestHandler<GetWebhooksQuery, GetWebhooksResult>
{
    private readonly IAppDbContext _db;

    public GetWebhooksHandler(IAppDbContext db) => _db = db;

    public async Task<GetWebhooksResult> Handle(GetWebhooksQuery request, CancellationToken ct)
    {
        var subs = await _db.WebhookSubscriptions
            .Include(s => s.Deliveries)
            .Select(s => new WebhookDto(
                s.Id,
                s.Name,
                s.Url,
                s.EventType.ToString(),
                s.IsActive,
                s.Deliveries.Count))
            .ToListAsync(ct);

        return new GetWebhooksResult(subs);
    }
}
