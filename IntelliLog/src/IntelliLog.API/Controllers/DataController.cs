using IntelliLog.Application.Commands.GenerateData;
using IntelliLog.Application.Commands.TrainModel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliLog.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly IMediator _mediator;

    public DataController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Generate synthetic log and document data for testing/training.
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<GenerateDataResult>> Generate(
        [FromQuery] int logs = 100,
        [FromQuery] int documents = 20)
    {
        var result = await _mediator.Send(new GenerateDataCommand(logs, documents));
        return Ok(result);
    }

    /// <summary>
    /// Train an ML.NET model on existing data. ModelType: "log" or "document".
    /// </summary>
    [HttpPost("train")]
    public async Task<ActionResult<TrainModelResult>> Train([FromBody] TrainModelCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
