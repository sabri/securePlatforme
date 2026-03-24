using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntelliLog.Infrastructure.Data;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<LogEntry> LogEntries => Set<LogEntry>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Severity).HasConversion<string>();
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.Severity);
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Category).HasConversion<string>();
            // Store embedding as a JSON blob for SQLite compatibility
            e.Ignore(x => x.Embedding);
        });

        modelBuilder.Entity<WebhookSubscription>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasConversion<string>();
            e.HasMany(x => x.Deliveries)
             .WithOne()
             .HasForeignKey(x => x.SubscriptionId);
        });

        modelBuilder.Entity<WebhookDelivery>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.Status);
        });
    }
}
