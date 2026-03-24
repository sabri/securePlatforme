using IntelliLog.Domain.Entities;

namespace IntelliLog.Application.Common.Interfaces;

public interface IDataGenerator
{
    List<LogEntry> GenerateLogs(int count);
    List<Document> GenerateDocuments(int count);
}
