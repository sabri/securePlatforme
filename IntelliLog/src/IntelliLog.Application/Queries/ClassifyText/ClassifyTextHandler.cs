using IntelliLog.Application.Common.Interfaces;
using MediatR;

namespace IntelliLog.Application.Queries.ClassifyText;

public class ClassifyTextHandler : IRequestHandler<ClassifyTextQuery, ClassifyTextResult>
{
    private readonly IMLService _ml;

    public ClassifyTextHandler(IMLService ml) => _ml = ml;

    public Task<ClassifyTextResult> Handle(ClassifyTextQuery request, CancellationToken ct)
    {
        var result = request.ModelType.ToLowerInvariant() switch
        {
            "log" when _ml.IsLogModelTrained =>
                new ClassifyTextResult(request.Text, _ml.ClassifyLogSeverity(request.Text).ToString(), true),
            "document" when _ml.IsDocumentModelTrained =>
                new ClassifyTextResult(request.Text, _ml.ClassifyDocument(request.Text).ToString(), true),
            _ => new ClassifyTextResult(request.Text, "N/A", false)
        };

        return Task.FromResult(result);
    }
}
