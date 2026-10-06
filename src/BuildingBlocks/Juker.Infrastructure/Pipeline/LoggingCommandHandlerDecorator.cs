using Juker.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Juker.Infrastructure.Pipeline;

internal sealed class LoggingCommandHandlerDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    ILogger<LoggingCommandHandlerDecorator<TCommand, TResult>> logger)
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private static readonly Action<ILogger, string, Exception?> LogExecuting =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(LogExecuting)), "Executando comando {CommandType}");

    private static readonly Action<ILogger, string, string?, string?, Exception?> LogFailed =
        LoggerMessage.Define<string, string?, string?>(LogLevel.Warning, new EventId(2, nameof(LogFailed)), "Comando {CommandType} falhou com erro {ErrorCode}: {ErrorMessage}");

    private static readonly Action<ILogger, string, Exception?> LogCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, nameof(LogCompleted)), "Comando {CommandType} concluído com sucesso");

    public async Task<Result<TResult>> Handle(TCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        LogExecuting(logger, typeof(TCommand).Name, null);
        var result = await inner.Handle(command, ct).ConfigureAwait(false);
        if (result.IsFailure)
        {
            LogFailed(logger, typeof(TCommand).Name, result.Error?.Code, result.Error?.Message, null);
        }
        else
        {
            LogCompleted(logger, typeof(TCommand).Name, null);
        }

        return result;
    }
}
