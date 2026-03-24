using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntelliLog.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior: logs the duration of every request
/// and warns if a request takes longer than 500ms.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("[CQRS] Handling {RequestName}", requestName);

        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > 500)
            _logger.LogWarning("[CQRS] {RequestName} took {Elapsed}ms (slow!)", requestName, sw.ElapsedMilliseconds);
        else
            _logger.LogInformation("[CQRS] {RequestName} completed in {Elapsed}ms", requestName, sw.ElapsedMilliseconds);

        return response;
    }
}
