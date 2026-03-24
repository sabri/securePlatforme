using IntelliLog.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IntelliLog.Application.Queries.SearchKnowledgeBase;

public class SearchKnowledgeBaseHandler : IRequestHandler<SearchKnowledgeBaseQuery, SearchKnowledgeBaseResult>
{
    private readonly IAppDbContext _db;
    private readonly IVectorStore _vectorStore;

    public SearchKnowledgeBaseHandler(IAppDbContext db, IVectorStore vectorStore)
    {
        _db = db;
        _vectorStore = vectorStore;
    }

    public async Task<SearchKnowledgeBaseResult> Handle(SearchKnowledgeBaseQuery request, CancellationToken ct)
    {
        // [RAG] Embed the query text
        var queryEmbedding = _vectorStore.Embed(request.Query);

        // [RAG] Search the vector store for similar document embeddings
        var results = _vectorStore.Search(queryEmbedding, request.TopK);

        var hits = new List<SearchHit>();
        foreach (var (docId, score) in results)
        {
            var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == docId, ct);
            if (doc is null) continue;

            var snippet = doc.Content.Length > 200
                ? doc.Content[..200] + "..."
                : doc.Content;

            hits.Add(new SearchHit(doc.Id, doc.Title, doc.Category.ToString(), snippet, score));
        }

        return new SearchKnowledgeBaseResult(hits);
    }
}
