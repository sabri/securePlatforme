namespace SecurePlatform.Application.Interfaces;

public interface IAiService
{
    Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamCompletionAsync(string prompt, CancellationToken cancellationToken = default);

    Task<string> QueryKnowledgeBaseAsync(string question, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default);

    Task SetModelAsync(string modelName, CancellationToken cancellationToken = default);

    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
