using IntelliLog.Application.Commands.IngestDocument;
using IntelliLog.Application.Queries.GetDocuments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliLog.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<ActionResult<IngestDocumentResult>> Ingest([FromBody] IngestDocumentCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<GetDocumentsResult>> GetDocuments(
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetDocumentsQuery(category, page, pageSize));
        return Ok(result);
    }
}
