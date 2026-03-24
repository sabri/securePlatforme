using MediatR;

namespace IntelliLog.Application.Queries.DetectAnomalies;

public record DetectAnomaliesQuery(int WindowSize = 100) : IRequest<DetectAnomaliesResult>;

public record DetectAnomaliesResult(int TotalChecked, int AnomaliesFound, List<AnomalyDto> Anomalies);

public record AnomalyDto(Guid LogId, DateTime Timestamp, string Message, double AnomalyScore);
