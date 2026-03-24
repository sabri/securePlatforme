using MediatR;

namespace IntelliLog.Application.Commands.GenerateData;

public record GenerateDataCommand(int LogCount, int DocumentCount) : IRequest<GenerateDataResult>;

public record GenerateDataResult(int LogsGenerated, int DocumentsGenerated);
