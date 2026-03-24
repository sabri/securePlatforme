using MediatR;

namespace IntelliLog.Application.Commands.IngestDocument;

public record IngestDocumentCommand(string Title, string Content) : IRequest<IngestDocumentResult>;

public record IngestDocumentResult(Guid DocumentId, string Category, string Title);
