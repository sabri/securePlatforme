using IntelliLog.Application.Commands.IngestLogs;
using IntelliLog.Application.Queries.DetectAnomalies;
using IntelliLog.Application.Queries.GetLogs;
using IntelliLog.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliLog.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LogsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("ingest")]
    public async Task<ActionResult<IngestLogsResult>> Ingest([FromBody] IngestLogsCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<GetLogsResult>> GetLogs(
        [FromQuery] string? severity = null,
        [FromQuery] string? source = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        LogSeverity? sev = null;
        if (!string.IsNullOrEmpty(severity) && Enum.TryParse<LogSeverity>(severity, true, out var parsed))
            sev = parsed;

        var result = await _mediator.Send(new GetLogsQuery(sev, source, page, pageSize));
        return Ok(result);
    }

    [HttpPost("detect-anomalies")]
    public async Task<ActionResult<DetectAnomaliesResult>> DetectAnomalies([FromQuery] int windowSize = 100)
    {
        var result = await _mediator.Send(new DetectAnomaliesQuery(windowSize));
        return Ok(result);
    }
}
