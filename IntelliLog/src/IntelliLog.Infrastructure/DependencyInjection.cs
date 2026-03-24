using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Infrastructure.Data;
using IntelliLog.Infrastructure.DataGeneration;
using IntelliLog.Infrastructure.ML;
using IntelliLog.Infrastructure.VectorStore;
using IntelliLog.Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntelliLog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // EF Core with SQLite
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // ML.NET service (singleton — models live in memory)
        services.AddSingleton<IMLService, MLService>();

        // Vector store (singleton — in-memory index)
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();

        // Webhook engine
        var webhookService = new WebhookService();
        services.AddSingleton(webhookService);
        services.AddSingleton<IWebhookService>(webhookService);
        services.AddHostedService<WebhookDeliveryWorker>();
        services.AddHttpClient("WebhookClient");

        // Data generator
        services.AddTransient<IDataGenerator, SyntheticDataGenerator>();

        return services;
    }
}
