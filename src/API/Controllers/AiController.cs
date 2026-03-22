using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurePlatform.Application.Interfaces;

namespace SecurePlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // All AI endpoints require authentication
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    /// <summary>
    /// Send a prompt to the AI service.
    /// POST /api/ai/complete
    /// </summary>
    [HttpPost("complete")]
    public async Task<IActionResult> GetCompletion([FromBody] PromptRequest request, CancellationToken ct)
    {
        var result = await _aiService.GetCompletionAsync(request.Prompt, ct);
        return Ok(new { response = result });
    }

    /// <summary>
    /// Query the RAG knowledge base.
    /// POST /api/ai/ask
    /// </summary>
    [HttpPost("ask")]
    public async Task<IActionResult> AskKnowledgeBase([FromBody] PromptRequest request, CancellationToken ct)
    {
        var result = await _aiService.QueryKnowledgeBaseAsync(request.Prompt, ct);
        return Ok(new { response = result });
    }
}

public record PromptRequest(string Prompt);
