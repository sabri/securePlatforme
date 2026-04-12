using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
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
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}

/// <summary>
/// In-memory vector store for RAG. Stores text chunks with their embeddings.
/// </summary>
public class VectorStore
{
    private readonly ConcurrentBag<(string Text, float[] Embedding)> _entries = new();

    public void Add(string text, float[] embedding)
    {
        _entries.Add((text, embedding));
    }

    public IReadOnlyList<string> Search(float[] queryEmbedding, int topK = 3)
    {
        return _entries
            .Select(entry => (entry.Text, Score: CosineSimilarity(queryEmbedding, entry.Embedding)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Text)
            .ToList();
    }

    public int Count => _entries.Count;

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;

        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denominator == 0 ? 0f : dot / denominator;
    }
}

public class AiService : IAiService
{
    private readonly OllamaApiClient _client;
    private readonly OllamaSettings _settings;
    private readonly ILogger<AiService> _logger;
    private readonly VectorStore _vectorStore;
    private string _currentModel;

    public AiService(IOptions<OllamaSettings> options, ILogger<AiService> logger, VectorStore vectorStore)
    {
        _settings = options.Value;
        _logger = logger;
        _vectorStore = vectorStore;
        _client = new OllamaApiClient(new Uri(_settings.BaseUrl));
        _currentModel = _settings.Model;
    }

    // ── Completion ────────────────────────────────────────────

    public async Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GenerateAsync(new OllamaSharp.Models.GenerateRequest
            {
                Model = _currentModel,
                Prompt = prompt,
                Stream = false
            }, cancellationToken).StreamToEndAsync();

            return response?.Response ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama completion failed for model {Model}", _currentModel);
            throw;
        }
    }

    // ── Streaming ─────────────────────────────────────────────

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stream = _client.GenerateAsync(new OllamaSharp.Models.GenerateRequest
        {
            Model = _currentModel,
            Prompt = prompt,
            Stream = true
        }, cancellationToken);

        await foreach (var chunk in stream.WithCancellation(cancellationToken))
        {
            if (chunk?.Response is not null)
                yield return chunk.Response;
        }
    }

    // ── Model Management ──────────────────────────────────────

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var models = await _client.ListLocalModelsAsync(cancellationToken);
        return models.Select(m => m.Name).ToList();
    }

    public Task SetModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        _currentModel = modelName;
        _logger.LogInformation("Active model switched to {Model}", modelName);
        return Task.CompletedTask;
    }

    // ── Embeddings ────────────────────────────────────────────

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.EmbedAsync(new OllamaSharp.Models.EmbedRequest
            {
                Model = _settings.EmbeddingModel,
                Input = [text]
            }, cancellationToken);

            return response?.Embeddings?.FirstOrDefault()?.ToArray() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding generation failed for model {Model}", _settings.EmbeddingModel);
            throw;
        }
    }

    // ── RAG Knowledge Base ────────────────────────────────────

    public async Task<string> QueryKnowledgeBaseAsync(string question, CancellationToken cancellationToken = default)
    {
        string context = string.Empty;

        // If the vector store has documents, retrieve relevant chunks
        if (_vectorStore.Count > 0)
        {
            var queryEmbedding = await GetEmbeddingAsync(question, cancellationToken);
            var relevantChunks = _vectorStore.Search(queryEmbedding, topK: 3);
            context = string.Join("\n\n", relevantChunks);
        }

        var ragPrompt = string.IsNullOrEmpty(context)
            ? $"""
                You are a helpful security platform assistant. Answer the following question 
                concisely and accurately. If you don't know, say so.

                Question: {question}
                """
            : $"""
                You are a helpful security platform assistant. Use the following context to answer 
                the question. If the context doesn't contain the answer, say so.

                Context:
                {context}

                Question: {question}
                """;

        return await GetCompletionAsync(ragPrompt, cancellationToken);
    }
}
