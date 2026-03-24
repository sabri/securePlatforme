using IntelliLog.Domain.Enums;

namespace IntelliLog.Domain.Entities;

public class LogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogSeverity Severity { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsAnomaly { get; set; }
    public double? AnomalyScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
