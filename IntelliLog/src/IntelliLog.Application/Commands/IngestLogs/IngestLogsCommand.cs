using IntelliLog.Domain.Enums;
using MediatR;

namespace IntelliLog.Application.Commands.IngestLogs;

/// <summary>
/// CQRS Command: Ingest a batch of log entries, classify severity
/// via ML.NET, detect anomalies, and trigger webhooks if critical.
/// </summary>
public record IngestLogsCommand(List<LogInput> Logs) : IRequest<IngestLogsResult>;

public record LogInput(string Source, string Message, DateTime? Timestamp = null);

public record IngestLogsResult(
    int Ingested,
    int AnomaliesDetected,
    int CriticalCount);
