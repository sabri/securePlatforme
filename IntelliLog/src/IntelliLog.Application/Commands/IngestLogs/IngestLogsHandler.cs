using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Domain.Entities;
using IntelliLog.Domain.Enums;
using MediatR;

namespace IntelliLog.Application.Commands.IngestLogs;

public class IngestLogsHandler : IRequestHandler<IngestLogsCommand, IngestLogsResult>
{
    private readonly IAppDbContext _db;
    private readonly IMLService _ml;
    private readonly IWebhookService _webhooks;

    public IngestLogsHandler(IAppDbContext db, IMLService ml, IWebhookService webhooks)
    {
        _db = db;
        _ml = ml;
        _webhooks = webhooks;
    }

    public async Task<IngestLogsResult> Handle(IngestLogsCommand request, CancellationToken ct)
    {
        var entries = new List<LogEntry>();
        int criticalCount = 0;

        foreach (var log in request.Logs)
        {
            // [ML.NET] Classify log severity using the trained model
            var severity = _ml.IsLogModelTrained
                ? _ml.ClassifyLogSeverity(log.Message)
                : GuessFromKeywords(log.Message);

            var entry = new LogEntry
            {
                Timestamp = log.Timestamp ?? DateTime.UtcNow,
                Source = log.Source,
                Message = log.Message,
                Severity = severity
            };

            entries.Add(entry);

            if (severity == LogSeverity.Critical)
                criticalCount++;
        }

        _db.LogEntries.AddRange(entries);
        await _db.SaveChangesAsync(ct);

        // [WEBHOOK] Fire alert for critical logs
        if (criticalCount > 0)
        {
            await _webhooks.QueueEventAsync(WebhookEventType.CriticalLogReceived, new
            {
                count = criticalCount,
                messages = entries
                    .Where(e => e.Severity == LogSeverity.Critical)
                    .Select(e => e.Message)
                    .ToList(),
                timestamp = DateTime.UtcNow
            }, ct);
        }

        return new IngestLogsResult(entries.Count, 0, criticalCount);
    }

    private static LogSeverity GuessFromKeywords(string message)
    {
        var lower = message.ToLowerInvariant();
        if (lower.Contains("critical") || lower.Contains("unreachable") || lower.Contains("out of memory"))
            return LogSeverity.Critical;
        if (lower.Contains("error") || lower.Contains("exception") || lower.Contains("failed"))
            return LogSeverity.Error;
        if (lower.Contains("warning") || lower.Contains("retry") || lower.Contains("timeout"))
            return LogSeverity.Warning;
        return LogSeverity.Info;
    }
}
