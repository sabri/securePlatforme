using IntelliLog.Domain.Entities;
using IntelliLog.Domain.Enums;

namespace IntelliLog.Application.Common.Interfaces;

public interface IMLService
{
    LogSeverity ClassifyLogSeverity(string message);
    DocumentCategory ClassifyDocument(string text);

    /// <summary>
    /// Run anomaly detection on a batch of log entries.
    /// Sets IsAnomaly and AnomalyScore on each entry and returns detected anomalies.
    /// </summary>
    List<LogEntry> DetectAnomalies(List<LogEntry> logs);

    Task TrainLogClassifierAsync(List<(string Text, string Label)> data, CancellationToken ct = default);
    Task TrainDocumentClassifierAsync(List<(string Text, string Label)> data, CancellationToken ct = default);

    bool IsLogModelTrained { get; }
    bool IsDocumentModelTrained { get; }
}
