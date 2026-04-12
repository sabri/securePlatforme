using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecurePlatform.Application.Interfaces;
using SecurePlatform.AI.Services;

namespace SecurePlatform.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OllamaSettings>(configuration.GetSection(OllamaSettings.SectionName));
        services.AddSingleton<VectorStore>();
        services.AddScoped<IAiService, AiService>();
        return services;
    }
}
