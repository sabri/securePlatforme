using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IntelliLog.Application.Commands.TrainModel;

public class TrainModelHandler : IRequestHandler<TrainModelCommand, TrainModelResult>
{
    private readonly IAppDbContext _db;
    private readonly IMLService _ml;
    private readonly IWebhookService _webhooks;

    public TrainModelHandler(IAppDbContext db, IMLService ml, IWebhookService webhooks)
    {
        _db = db;
        _ml = ml;
        _webhooks = webhooks;
    }

    public async Task<TrainModelResult> Handle(TrainModelCommand request, CancellationToken ct)
    {
        switch (request.ModelType.ToLowerInvariant())
        {
            case "log":
            {
                var logs = await _db.LogEntries.ToListAsync(ct);
                if (logs.Count < 10)
                    return new TrainModelResult("log", false, "Need at least 10 log entries to train.");

                var trainingData = logs.Select(l => (l.Message, l.Severity.ToString())).ToList();
                await _ml.TrainLogClassifierAsync(trainingData, ct);

                await _webhooks.QueueEventAsync(WebhookEventType.ModelTrained, new
                {
                    modelType = "log",
                    trainingRecords = logs.Count,
                    timestamp = DateTime.UtcNow
                }, ct);

                return new TrainModelResult("log", true, $"Log classifier trained on {logs.Count} records.");
            }
            case "document":
            {
                var docs = await _db.Documents.ToListAsync(ct);
                if (docs.Count < 10)
                    return new TrainModelResult("document", false, "Need at least 10 documents to train.");

                var trainingData = docs.Select(d => (d.Content, d.Category.ToString())).ToList();
                await _ml.TrainDocumentClassifierAsync(trainingData, ct);

                await _webhooks.QueueEventAsync(WebhookEventType.ModelTrained, new
                {
                    modelType = "document",
                    trainingRecords = docs.Count,
                    timestamp = DateTime.UtcNow
                }, ct);

                return new TrainModelResult("document", true, $"Document classifier trained on {docs.Count} records.");
            }
            default:
                return new TrainModelResult(request.ModelType, false, $"Unknown model type '{request.ModelType}'. Use 'log' or 'document'.");
        }
    }
}
