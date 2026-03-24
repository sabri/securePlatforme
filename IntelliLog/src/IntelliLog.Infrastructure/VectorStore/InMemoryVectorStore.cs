using System.Collections.Concurrent;
using IntelliLog.Application.Common.Interfaces;

namespace IntelliLog.Infrastructure.VectorStore;

/// <summary>
/// In-memory vector store using TF-IDF-style bag-of-words embeddings
/// and cosine similarity for RAG document retrieval.
/// No external vector DB required.
/// </summary>
public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<Guid, float[]> _vectors = new();
    private readonly ConcurrentDictionary<string, int> _vocabulary = new();
    private int _vocabIndex;

    public int Count => _vectors.Count;

    public void Index(Guid documentId, float[] embedding)
    {
        _vectors[documentId] = embedding;
    }

    public void Remove(Guid documentId)
    {
        _vectors.TryRemove(documentId, out _);
    }

    public List<(Guid DocumentId, double Score)> Search(float[] queryEmbedding, int topK = 5)
    {
        if (_vectors.IsEmpty) return new List<(Guid, double)>();

        return _vectors
            .Select(kv => (DocumentId: kv.Key, Score: CosineSimilarity(queryEmbedding, kv.Value)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Where(x => x.Score > 0.01) // Filter near-zero matches
            .ToList();
    }

    public float[] Embed(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        var tokens = Tokenize(text);

        // Build vocabulary as we discover new words
        foreach (var token in tokens)
        {
            _vocabulary.GetOrAdd(token, _ => Interlocked.Increment(ref _vocabIndex));
        }

        // Create bag-of-words vector
        var vector = new float[_vocabIndex + 1];
        var tokenCounts = tokens.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

        foreach (var (token, count) in tokenCounts)
        {
            if (_vocabulary.TryGetValue(token, out var idx) && idx < vector.Length)
            {
                vector[idx] = count;
            }
        }

        // L2 normalize
        var magnitude = (float)Math.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
                vector[i] /= magnitude;
        }

        return vector;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        int len = Math.Min(a.Length, b.Length);
        double dot = 0, magA = 0, magB = 0;

        for (int i = 0; i < len; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        // Account for remaining dimensions
        for (int i = len; i < a.Length; i++) magA += a[i] * a[i];
        for (int i = len; i < b.Length; i++) magB += b[i] * b[i];

        var denom = Math.Sqrt(magA) * Math.Sqrt(magB);
        return denom < 1e-10 ? 0.0 : dot / denom;
    }

    private static string[] Tokenize(string text)
    {
        return text
            .ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2) // Skip very short tokens
            .ToArray();
    }
}
