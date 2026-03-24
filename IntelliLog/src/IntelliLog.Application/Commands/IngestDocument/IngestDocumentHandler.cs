using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Domain.Entities;
using IntelliLog.Domain.Enums;
using MediatR;

namespace IntelliLog.Application.Commands.IngestDocument;

public class IngestDocumentHandler : IRequestHandler<IngestDocumentCommand, IngestDocumentResult>
{
    private readonly IAppDbContext _db;
    private readonly IMLService _ml;
    private readonly IVectorStore _vectorStore;
    private readonly IWebhookService _webhooks;

    public IngestDocumentHandler(IAppDbContext db, IMLService ml, IVectorStore vectorStore, IWebhookService webhooks)
    {
        _db = db;
        _ml = ml;
        _vectorStore = vectorStore;
        _webhooks = webhooks;
    }

    public async Task<IngestDocumentResult> Handle(IngestDocumentCommand request, CancellationToken ct)
    {
        // [ML.NET] Classify the document category
        var category = _ml.IsDocumentModelTrained
            ? _ml.ClassifyDocument(request.Content)
            : DocumentCategory.General;

        // [RAG] Generate embedding and index in vector store
        var embedding = _vectorStore.Embed(request.Content);

        var doc = new Document
        {
            Title = request.Title,
            Content = request.Content,
            Category = category,
            Embedding = embedding
        };

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct);

        // Index in vector store for RAG search
        _vectorStore.Index(doc.Id, embedding);

        // [WEBHOOK] Notify subscribers
        await _webhooks.QueueEventAsync(WebhookEventType.DocumentIngested, new
        {
            documentId = doc.Id,
            title = doc.Title,
            category = category.ToString(),
            timestamp = DateTime.UtcNow
        }, ct);

        return new IngestDocumentResult(doc.Id, category.ToString(), doc.Title);
    }
}
