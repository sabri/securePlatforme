using Microsoft.Extensions.DependencyInjection;
using SecurePlatform.Application.Interfaces;
using SecurePlatform.AI.Services;

namespace SecurePlatform.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        services.AddScoped<IAiService, AiService>();
        return services;
    }
}
