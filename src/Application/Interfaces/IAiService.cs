namespace SecurePlatform.Application.Interfaces;

/// <summary>
/// Placeholder interface for AI/RAG/LLM services.
/// You'll implement this when integrating Semantic Kernel, LangChain, etc.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Send a prompt and get a completion response.
    /// </summary>
    Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// RAG: Query your documents/knowledge base with context-aware retrieval.
    /// </summary>
    Task<string> QueryKnowledgeBaseAsync(string question, CancellationToken cancellationToken = default);
}
