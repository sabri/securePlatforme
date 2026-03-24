using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IntelliLog.Application.Queries.GetDocuments;

public class GetDocumentsHandler : IRequestHandler<GetDocumentsQuery, GetDocumentsResult>
{
    private readonly IAppDbContext _db;

    public GetDocumentsHandler(IAppDbContext db) => _db = db;

    public async Task<GetDocumentsResult> Handle(GetDocumentsQuery request, CancellationToken ct)
    {
        var query = _db.Documents.AsQueryable();

        if (!string.IsNullOrEmpty(request.Category) &&
            Enum.TryParse<DocumentCategory>(request.Category, true, out var cat))
        {
            query = query.Where(d => d.Category == cat);
        }

        var totalCount = await query.CountAsync(ct);

        var docs = await query
            .OrderByDescending(d => d.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new DocumentDto(
                d.Id,
                d.Title,
                d.Category.ToString(),
                d.Content.Length > 200 ? d.Content.Substring(0, 200) + "..." : d.Content))
            .ToListAsync(ct);

        return new GetDocumentsResult(docs, totalCount);
    }
}
