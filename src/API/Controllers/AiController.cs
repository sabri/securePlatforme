using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurePlatform.Application.Interfaces;

namespace SecurePlatform.API.Controllers;

// ═══════════════════════════════════════════════════════════════
// [SECURITY: RATE LIMITING] — AI endpoints are expensive. The
// "ai" rate-limit policy (10 req / 60s per IP) prevents abuse
// and protects against denial-of-service via costly completions.
// ═══════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════
// [BFF PATTERN] — The React SPA calls /api/ai/* through the same
// origin via the BFF proxy. Auth cookies are sent automatically;
// no token is exposed to client-side JavaScript.
// ═══════════════════════════════════════════════════════════════
[ApiController]
[Route("api/[controller]")]
[Authorize] // All AI endpoints require authentication
[EnableRateLimiting("ai")]
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
        // ═══════════════════════════════════════════════════════
        // [SECURITY: XSS] — Validate prompt input to reject
        // payloads containing HTML/script injection attempts.
        // The AI response is also returned as data (not HTML),
        // and React auto-escapes rendered output.
        // ═══════════════════════════════════════════════════════
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 4000)
            return BadRequest(new { message = "Invalid prompt." });

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
        // [SECURITY: XSS] — Input length validation
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 4000)
            return BadRequest(new { message = "Invalid prompt." });

        var result = await _aiService.QueryKnowledgeBaseAsync(request.Prompt, ct);
        return Ok(new { response = result });
    }
}

public record PromptRequest(string Prompt);
