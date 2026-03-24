using MediatR;

namespace IntelliLog.Application.Queries.GetDocuments;

public record GetDocumentsQuery(
    string? Category = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<GetDocumentsResult>;

public record GetDocumentsResult(List<DocumentDto> Documents, int TotalCount);

public record DocumentDto(Guid Id, string Title, string Category, string Snippet);
