using IntelliLog.Application.Commands.RegisterWebhook;
using IntelliLog.Application.Queries.GetWebhooks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliLog.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;

    public WebhooksController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<ActionResult<RegisterWebhookResult>> Register([FromBody] RegisterWebhookCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<GetWebhooksResult>> GetAll()
    {
        var result = await _mediator.Send(new GetWebhooksQuery());
        return Ok(result);
    }
}
