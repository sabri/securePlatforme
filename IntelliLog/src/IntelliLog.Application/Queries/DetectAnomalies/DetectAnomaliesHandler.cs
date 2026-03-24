using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IntelliLog.Application.Queries.DetectAnomalies;

public class DetectAnomaliesHandler : IRequestHandler<DetectAnomaliesQuery, DetectAnomaliesResult>
{
    private readonly IAppDbContext _db;
    private readonly IMLService _ml;
    private readonly IWebhookService _webhooks;

    public DetectAnomaliesHandler(IAppDbContext db, IMLService ml, IWebhookService webhooks)
    {
        _db = db;
        _ml = ml;
        _webhooks = webhooks;
    }

    public async Task<DetectAnomaliesResult> Handle(DetectAnomaliesQuery request, CancellationToken ct)
    {
        var recentLogs = await _db.LogEntries
            .OrderByDescending(l => l.Timestamp)
            .Take(request.WindowSize)
            .ToListAsync(ct);

        if (recentLogs.Count < 5)
            return new DetectAnomaliesResult(recentLogs.Count, 0, new List<AnomalyDto>());

        // [ML.NET] Run anomaly detection on recent log entries
        var anomalies = _ml.DetectAnomalies(recentLogs);

        // Update the log entries with anomaly info
        foreach (var log in recentLogs.Where(l => l.IsAnomaly))
        {
            _db.LogEntries.Update(log);
        }
        await _db.SaveChangesAsync(ct);

        // [WEBHOOK] Fire alert if anomalies detected
        if (anomalies.Count > 0)
        {
            await _webhooks.QueueEventAsync(WebhookEventType.AnomalyDetected, new
            {
                anomalyCount = anomalies.Count,
                window = request.WindowSize,
                topAnomalies = anomalies.Take(5).Select(a => new
                {
                    logId = a.Id,
                    message = a.Message,
                    score = a.AnomalyScore
                }),
                timestamp = DateTime.UtcNow
            }, ct);
        }

        var dtos = anomalies.Select(a =>
            new AnomalyDto(a.Id, a.Timestamp, a.Message, a.AnomalyScore ?? 0.0)).ToList();

        return new DetectAnomaliesResult(recentLogs.Count, anomalies.Count, dtos);
    }
}
