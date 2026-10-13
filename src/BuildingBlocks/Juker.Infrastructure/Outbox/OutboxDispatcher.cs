using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juker.Infrastructure.Outbox;

internal sealed class OutboxDispatcher(
    OutboxProcessor processor,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher> logger)
    : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogDispatcherError =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, nameof(LogDispatcherError)), "Erro inesperado ao executar o despacho do outbox");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(options.Value.IntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await processor.ProcessAllAsync(drainAll: false, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogDispatcherError(logger, ex);
            }

            try
            {
                await Task.Delay(interval, timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
