using MediatR;

namespace IntelliLog.Application.Queries.SearchKnowledgeBase;

public record SearchKnowledgeBaseQuery(string Query, int TopK = 5) : IRequest<SearchKnowledgeBaseResult>;

public record SearchKnowledgeBaseResult(List<SearchHit> Hits);

public record SearchHit(Guid DocumentId, string Title, string Category, string Snippet, double Score);
