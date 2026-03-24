using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Domain.Entities;
using MediatR;

namespace IntelliLog.Application.Commands.GenerateData;

public class GenerateDataHandler : IRequestHandler<GenerateDataCommand, GenerateDataResult>
{
    private readonly IAppDbContext _db;
    private readonly IDataGenerator _generator;
    private readonly IVectorStore _vectorStore;

    public GenerateDataHandler(IAppDbContext db, IDataGenerator generator, IVectorStore vectorStore)
    {
        _db = db;
        _generator = generator;
        _vectorStore = vectorStore;
    }

    public async Task<GenerateDataResult> Handle(GenerateDataCommand request, CancellationToken ct)
    {
        // Generate synthetic logs
        var logs = _generator.GenerateLogs(request.LogCount);
        _db.LogEntries.AddRange(logs);

        // Generate synthetic documents and embed them for RAG
        var docs = _generator.GenerateDocuments(request.DocumentCount);
        foreach (var doc in docs)
        {
            var embedding = _vectorStore.Embed(doc.Content);
            doc.Embedding = embedding;
        }

        _db.Documents.AddRange(docs);
        await _db.SaveChangesAsync(ct);

        // Index all documents in vector store
        foreach (var doc in docs)
        {
            _vectorStore.Index(doc.Id, doc.Embedding!);
        }

        return new GenerateDataResult(logs.Count, docs.Count);
    }
}
