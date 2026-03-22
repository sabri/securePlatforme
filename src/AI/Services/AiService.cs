using SecurePlatform.Application.Interfaces;

namespace SecurePlatform.AI.Services;

/// <summary>
/// Placeholder AI service — returns mock responses.
/// 
/// NEXT STEPS to make this real:
/// ─────────────────────────────────────────────
/// 1. Install Microsoft.SemanticKernel NuGet package
/// 2. Or use Azure.AI.OpenAI for direct OpenAI/Azure calls
/// 3. For RAG: use a vector DB (Qdrant, Chroma, Azure AI Search)
/// 4. Store embeddings of your documents
/// 5. On query: embed the question → find similar docs → send to LLM with context
/// 
/// Example integration points:
///   - Semantic Kernel: kernel.InvokePromptAsync(prompt)
///   - LangChain .NET: chain.RunAsync(question)
///   - Direct HTTP to OpenAI API
/// </summary>
public class AiService : IAiService
{
    public Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with real LLM call
        // Example with Semantic Kernel:
        // var result = await _kernel.InvokePromptAsync(prompt);
        // return result.GetValue<string>() ?? "";

        return Task.FromResult(
            $"[AI Placeholder] Received prompt: \"{prompt}\". " +
            "Integrate Semantic Kernel or OpenAI to get real responses.");
    }

    public Task<string> QueryKnowledgeBaseAsync(string question, CancellationToken cancellationToken = default)
    {
        // TODO: Implement RAG pipeline
        // 1. Embed the question using an embedding model
        // 2. Search vector DB for similar document chunks
        // 3. Build a prompt with retrieved context + question
        // 4. Send to LLM for answer generation

        return Task.FromResult(
            $"[RAG Placeholder] Question: \"{question}\". " +
            "Set up a vector database and embedding model to enable RAG.");
    }
}
