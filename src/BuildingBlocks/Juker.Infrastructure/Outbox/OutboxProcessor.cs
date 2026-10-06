using System.Reflection;
using System.Text.Json;
using Juker.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juker.Infrastructure.Outbox;

internal sealed class OutboxProcessor(
    IServiceProvider serviceProvider,
    OutboxRegistry registry,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static readonly Action<ILogger, string, Exception?> LogEventTypeNotFound =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, nameof(LogEventTypeNotFound)), "Tipo de evento '{EventType}' não encontrado no AppDomain");

    private static readonly Action<ILogger, Guid, Exception?> LogPayloadDeserializationFailed =
        LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(2, nameof(LogPayloadDeserializationFailed)), "Falha ao desserializar payload da mensagem {MessageId}");

    private static readonly Action<ILogger, string, Guid, Exception?> LogConsumerFailed =
        LoggerMessage.Define<string, Guid>(LogLevel.Warning, new EventId(3, nameof(LogConsumerFailed)), "Consumidor {Consumer} falhou ao processar mensagem {MessageId}");

    public async Task<int> ProcessAllAsync(bool drainAll, CancellationToken ct)
    {
        var totalProcessed = 0;
        var dbContextTypes = registry.DbContextTypes;

        foreach (var dbContextType in dbContextTypes)
        {
            var processed = await ProcessModuleAsync(dbContextType, drainAll, ct).ConfigureAwait(false);
            totalProcessed += processed;
        }

        return totalProcessed;
    }

    private async Task<int> ProcessModuleAsync(Type dbContextType, bool drainAll, CancellationToken ct)
    {
        var processedCount = 0;
        var maxAttempts = options.Value.MaxAttempts;

        while (!ct.IsCancellationRequested)
        {
            using var scope = serviceProvider.CreateScope();
            var db = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(dbContextType);

            var messages = await FetchMessagesAsync(db, drainAll, ct).ConfigureAwait(false);
            if (messages.Count == 0)
            {
                break;
            }

            var messageIds = messages.Select(m => m.Id).ToList();
            var inboxes = await db.Set<InboxMessage>()
                .Where(i => messageIds.Contains(i.MessageId))
                .Select(i => new ValueTuple<Guid, string>(i.MessageId, i.Consumer))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var existingInbox = new HashSet<(Guid MessageId, string Consumer)>(inboxes);

            var batchProcessed = 0;
            foreach (var message in messages)
            {
                var succeeded = await ProcessSingleMessageAsync(scope.ServiceProvider, db, message, existingInbox, maxAttempts, ct)
                    .ConfigureAwait(false);

                if (succeeded)
                {
                    batchProcessed++;
                }
            }

            if (batchProcessed > 0)
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            processedCount += batchProcessed;

            if (!drainAll || batchProcessed == 0)
            {
                break;
            }
        }

        return processedCount;
    }

    private Task<List<OutboxMessage>> FetchMessagesAsync(ModuleDbContext db, bool drainAll, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var query = db.Set<OutboxMessage>().Where(m => m.Status == "pending");

        if (!drainAll)
        {
            query = query.Where(m => m.NextAttemptAt <= now);
        }

        return query
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.Id)
            .Take(100)
            .ToListAsync(ct);
    }

    private async Task<bool> ProcessSingleMessageAsync(
        IServiceProvider sp,
        ModuleDbContext db,
        OutboxMessage message,
        HashSet<(Guid MessageId, string Consumer)> existingInbox,
        int maxAttempts,
        CancellationToken ct)
    {
        var eventType = Type.GetType(message.Type, throwOnError: false);
        if (eventType is null)
        {
            LogEventTypeNotFound(logger, message.Type, null);
            await FailPermanentlyAsync(db, message, $"Tipo '{message.Type}' não encontrado.", ct).ConfigureAwait(false);
            return false;
        }

        var integrationEvent = (IIntegrationEvent?)JsonSerializer.Deserialize(message.Payload, eventType, WebJson);
        if (integrationEvent is null)
        {
            LogPayloadDeserializationFailed(logger, message.Id, null);
            await FailPermanentlyAsync(db, message, "Falha ao desserializar payload.", ct).ConfigureAwait(false);
            return false;
        }

        var lastError = await DispatchToHandlersAsync(sp, db, message, eventType, integrationEvent, existingInbox, ct)
            .ConfigureAwait(false);

        if (lastError is null)
        {
            message.Status = "processed";
            message.ProcessedAt = timeProvider.GetUtcNow();
            return true;
        }

        await ApplyRetryPolicyAsync(db, message, lastError, maxAttempts, ct).ConfigureAwait(false);
        return false;
    }

    private async Task<Exception?> DispatchToHandlersAsync(
        IServiceProvider sp,
        ModuleDbContext db,
        OutboxMessage message,
        Type eventType,
        IIntegrationEvent integrationEvent,
        HashSet<(Guid MessageId, string Consumer)> existingInbox,
        CancellationToken ct)
    {
        var handlerInterfaceType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
        var handlers = sp.GetServices(handlerInterfaceType).ToList();
        Exception? lastError = null;

        foreach (var handler in handlers)
        {
            if (handler is null)
            {
                continue;
            }

            var consumerName = handler.GetType().FullName ?? handler.GetType().Name;
            if (existingInbox.Contains((message.Id, consumerName)))
            {
                continue;
            }

            try
            {
                var handleMethod = handlerInterfaceType.GetMethod(
                    nameof(IIntegrationEventHandler<IIntegrationEvent>.Handle),
                    [eventType, typeof(CancellationToken)])!;

                var task = (Task)handleMethod.Invoke(handler, [integrationEvent, ct])!;
                await task.ConfigureAwait(false);

                var inboxRecord = new InboxMessage
                {
                    MessageId = message.Id,
                    Consumer = consumerName,
                    ProcessedAt = timeProvider.GetUtcNow(),
                };

                db.Set<InboxMessage>().Add(inboxRecord);
                existingInbox.Add((message.Id, consumerName));
            }
            catch (Exception ex)
            {
                var unwrapped = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;
                LogConsumerFailed(logger, consumerName, message.Id, unwrapped);
                lastError = unwrapped;
            }
        }

        return lastError;
    }

    private static async Task FailPermanentlyAsync(ModuleDbContext db, OutboxMessage message, string error, CancellationToken ct)
    {
        message.Status = "failed";
        message.LastError = error;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task ApplyRetryPolicyAsync(
        ModuleDbContext db,
        OutboxMessage message,
        Exception lastError,
        int maxAttempts,
        CancellationToken ct)
    {
        message.Attempts++;
        message.LastError = lastError.ToString();

        if (message.Attempts >= maxAttempts)
        {
            message.Status = "failed";
        }
        else
        {
            var backoffSeconds = Math.Pow(2, message.Attempts - 1);
            message.NextAttemptAt = timeProvider.GetUtcNow().AddSeconds(backoffSeconds);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
