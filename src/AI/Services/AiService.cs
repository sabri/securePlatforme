using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using SecurePlatform.Application.Interfaces;

namespace SecurePlatform.AI.Services;

public class OllamaSettings
{
    public const string SectionName = "Ollama";
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2:1b";
}

public class AiService : IAiService
{
    private readonly OllamaApiClient _client;
    private readonly OllamaSettings _settings;
    private readonly ILogger<AiService> _logger;

    public AiService(IOptions<OllamaSettings> options, ILogger<AiService> logger)
    {
        _settings = options.Value;
        _logger = logger;
        _client = new OllamaApiClient(new Uri(_settings.BaseUrl));
    }

    public async Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GenerateAsync(new OllamaSharp.Models.GenerateRequest
            {
                Model = _settings.Model,
                Prompt = prompt,
                Stream = false
            }, cancellationToken).StreamToEndAsync();

            return response?.Response ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama completion failed for model {Model}", _settings.Model);
            throw;
        }
    }

    public async Task<string> QueryKnowledgeBaseAsync(string question, CancellationToken cancellationToken = default)
    {
        // RAG-style prompt: instruct the model to answer based on the question context.
        // For a full RAG pipeline, integrate a vector DB (Qdrant, Chroma) to retrieve
        // relevant document chunks and prepend them as context here.
        var ragPrompt = $"""
            You are a helpful security platform assistant. Answer the following question 
            concisely and accurately. If you don't know, say so.

            Question: {question}
            """;

        return await GetCompletionAsync(ragPrompt, cancellationToken);
    }
}
