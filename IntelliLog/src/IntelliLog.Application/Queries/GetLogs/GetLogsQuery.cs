using IntelliLog.Domain.Enums;
using MediatR;

namespace IntelliLog.Application.Queries.GetLogs;

public record GetLogsQuery(
    LogSeverity? Severity = null,
    string? Source = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<GetLogsResult>;

public record GetLogsResult(List<LogDto> Logs, int TotalCount);

public record LogDto(
    Guid Id,
    DateTime Timestamp,
    string Severity,
    string Source,
    string Message,
    bool IsAnomaly,
    double AnomalyScore
);
