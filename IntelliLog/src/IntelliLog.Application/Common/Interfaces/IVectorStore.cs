namespace IntelliLog.Application.Common.Interfaces;

public interface IVectorStore
{
    void Index(Guid documentId, float[] embedding);
    void Remove(Guid documentId);
    List<(Guid DocumentId, double Score)> Search(float[] queryEmbedding, int topK = 5);
    float[] Embed(string text);
    int Count { get; }
}
