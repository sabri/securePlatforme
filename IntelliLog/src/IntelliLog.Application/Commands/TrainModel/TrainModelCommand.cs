using MediatR;

namespace IntelliLog.Application.Commands.TrainModel;

public record TrainModelCommand(string ModelType) : IRequest<TrainModelResult>;

public record TrainModelResult(string ModelType, bool Success, string Message);
