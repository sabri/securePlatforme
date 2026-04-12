using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurePlatform.AI.Services;
using SecurePlatform.Application.Interfaces;

namespace SecurePlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("ai")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly VectorStore _vectorStore;

    public AiController(IAiService aiService, VectorStore vectorStore)
    {
        _aiService = aiService;
        _vectorStore = vectorStore;
    }

    /// <summary>
    /// Send a prompt to the AI service.
    /// POST /api/ai/complete
    /// </summary>
    [HttpPost("complete")]
    public async Task<IActionResult> GetCompletion([FromBody] PromptRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 4000)
            return BadRequest(new { message = "Invalid prompt." });

        var result = await _aiService.GetCompletionAsync(request.Prompt, ct);
        return Ok(new { response = result });
    }

    /// <summary>
    /// Stream a completion response via Server-Sent Events.
    /// POST /api/ai/stream
    /// </summary>
    [HttpPost("stream")]
    public async Task StreamCompletion([FromBody] PromptRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 4000)
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { message = "Invalid prompt." }, ct);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        await foreach (var token in _aiService.StreamCompletionAsync(request.Prompt, ct))
        {
            await Response.WriteAsync($"data: {token}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        await Response.WriteAsync("data: [DONE]\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    /// <summary>
    /// Query the RAG knowledge base.
    /// POST /api/ai/ask
    /// </summary>
    [HttpPost("ask")]
    public async Task<IActionResult> AskKnowledgeBase([FromBody] PromptRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 4000)
            return BadRequest(new { message = "Invalid prompt." });

        var result = await _aiService.QueryKnowledgeBaseAsync(request.Prompt, ct);
        return Ok(new { response = result });
    }

    /// <summary>
    /// List all locally available Ollama models.
    /// GET /api/ai/models
    /// </summary>
    [HttpGet("models")]
    public async Task<IActionResult> ListModels(CancellationToken ct)
    {
        var models = await _aiService.ListModelsAsync(ct);
        return Ok(new { models });
    }

    /// <summary>
    /// Switch the active model at runtime.
    /// POST /api/ai/models/switch
    /// </summary>
    [HttpPost("models/switch")]
    public async Task<IActionResult> SwitchModel([FromBody] SwitchModelRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
            return BadRequest(new { message = "Model name is required." });

        var available = await _aiService.ListModelsAsync(ct);
        if (!available.Contains(request.Model))
            return BadRequest(new { message = $"Model '{request.Model}' is not available locally. Pull it first with: ollama pull {request.Model}" });

        await _aiService.SetModelAsync(request.Model, ct);
        return Ok(new { message = $"Active model switched to '{request.Model}'." });
    }

    /// <summary>
    /// Generate an embedding vector for a given text.
    /// POST /api/ai/embed
    /// </summary>
    [HttpPost("embed")]
    public async Task<IActionResult> GetEmbedding([FromBody] PromptRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 8000)
            return BadRequest(new { message = "Invalid text for embedding." });

        var embedding = await _aiService.GetEmbeddingAsync(request.Prompt, ct);
        return Ok(new { embedding, dimensions = embedding.Length });
    }

    /// <summary>
    /// Ingest a text document into the RAG vector store.
    /// POST /api/ai/ingest
    /// </summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> IngestDocument([FromBody] IngestRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 50000)
            return BadRequest(new { message = "Invalid document text." });

        // Split text into chunks for better retrieval
        var chunks = ChunkText(request.Text, request.ChunkSize ?? 500);
        var ingested = 0;

        foreach (var chunk in chunks)
        {
            var embedding = await _aiService.GetEmbeddingAsync(chunk, ct);
            _vectorStore.Add(chunk, embedding);
            ingested++;
        }

        return Ok(new { message = $"Ingested {ingested} chunks into the knowledge base.", chunks = ingested });
    }

    private static List<string> ChunkText(string text, int chunkSize)
    {
        var chunks = new List<string>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = string.Empty;
        foreach (var paragraph in paragraphs)
        {
            if (currentChunk.Length + paragraph.Length > chunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.Trim());
                currentChunk = string.Empty;
            }
            currentChunk += paragraph + "\n\n";
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
            chunks.Add(currentChunk.Trim());

        return chunks;
    }
}

public record PromptRequest(string Prompt);
public record SwitchModelRequest(string Model);
public record IngestRequest(string Text, int? ChunkSize = 500);
