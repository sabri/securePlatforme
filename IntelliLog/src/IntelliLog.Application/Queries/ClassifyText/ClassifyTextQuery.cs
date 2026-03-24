using MediatR;

namespace IntelliLog.Application.Queries.ClassifyText;

public record ClassifyTextQuery(string Text, string ModelType) : IRequest<ClassifyTextResult>;

public record ClassifyTextResult(string Text, string PredictedLabel, bool ModelAvailable);
