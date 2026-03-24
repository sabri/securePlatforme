using IntelliLog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntelliLog.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<LogEntry> LogEntries { get; }
    DbSet<Document> Documents { get; }
    DbSet<WebhookSubscription> WebhookSubscriptions { get; }
    DbSet<WebhookDelivery> WebhookDeliveries { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
