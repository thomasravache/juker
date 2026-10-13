using Juker.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Juker.Infrastructure.Pipeline;

internal sealed class LoggingQueryHandlerDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<LoggingQueryHandlerDecorator<TQuery, TResult>> logger)
    : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private static readonly Action<ILogger, string, Exception?> LogExecuting =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(LogExecuting)), "Executando consulta {QueryType}");

    private static readonly Action<ILogger, string, string?, string?, Exception?> LogFailed =
        LoggerMessage.Define<string, string?, string?>(LogLevel.Warning, new EventId(2, nameof(LogFailed)), "Consulta {QueryType} falhou com erro {ErrorCode}: {ErrorMessage}");

    private static readonly Action<ILogger, string, Exception?> LogCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, nameof(LogCompleted)), "Consulta {QueryType} concluída com sucesso");

    public async Task<Result<TResult>> Handle(TQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        LogExecuting(logger, typeof(TQuery).Name, null);
        var result = await inner.Handle(query, ct).ConfigureAwait(false);
        if (result.IsFailure)
        {
            LogFailed(logger, typeof(TQuery).Name, result.Error?.Code, result.Error?.Message, null);
        }
        else
        {
            LogCompleted(logger, typeof(TQuery).Name, null);
        }

        return result;
    }
}
