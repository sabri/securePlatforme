using IntelliLog.Application.Queries.ClassifyText;
using IntelliLog.Application.Queries.SearchKnowledgeBase;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliLog.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IMediator _mediator;

    public SearchController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// RAG-powered semantic search across the document knowledge base.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SearchKnowledgeBaseResult>> Search(
        [FromQuery] string q,
        [FromQuery] int topK = 5)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Query parameter 'q' is required.");

        var result = await _mediator.Send(new SearchKnowledgeBaseQuery(q, topK));
        return Ok(result);
    }

    /// <summary>
    /// Classify text using a trained ML.NET model.
    /// </summary>
    [HttpPost("classify")]
    public async Task<ActionResult<ClassifyTextResult>> Classify([FromBody] ClassifyTextQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
