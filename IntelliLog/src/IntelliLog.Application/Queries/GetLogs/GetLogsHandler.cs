using IntelliLog.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IntelliLog.Application.Queries.GetLogs;

public class GetLogsHandler : IRequestHandler<GetLogsQuery, GetLogsResult>
{
    private readonly IAppDbContext _db;

    public GetLogsHandler(IAppDbContext db) => _db = db;

    public async Task<GetLogsResult> Handle(GetLogsQuery request, CancellationToken ct)
    {
        var query = _db.LogEntries.AsQueryable();

        if (request.Severity.HasValue)
            query = query.Where(l => l.Severity == request.Severity.Value);

        if (!string.IsNullOrEmpty(request.Source))
            query = query.Where(l => l.Source.Contains(request.Source));

        var totalCount = await query.CountAsync(ct);

        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new LogDto(
                l.Id, l.Timestamp, l.Severity.ToString(),
                l.Source, l.Message, l.IsAnomaly, l.AnomalyScore ?? 0.0))
            .ToListAsync(ct);

        return new GetLogsResult(logs, totalCount);
    }
}
