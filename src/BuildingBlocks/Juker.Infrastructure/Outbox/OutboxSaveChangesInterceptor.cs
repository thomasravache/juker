using System.Reflection;
using System.Text.Json;
using Juker.SharedKernel;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Juker.Infrastructure.Outbox;

internal sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        CaptureIntegrationEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        CaptureIntegrationEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void CaptureIntegrationEvents(Microsoft.EntityFrameworkCore.DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker.Entries().ToList();
        var outboxSet = context.Set<OutboxMessage>();

        foreach (var entry in entries)
        {
            if (TryGetEvents(entry.Entity, out var events, out var clearAction) && events.Count > 0)
            {
                foreach (var integrationEvent in events)
                {
                    var type = $"{integrationEvent.GetType().FullName}, {integrationEvent.GetType().Assembly.GetName().Name}";
                    var payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), WebJson);

                    var outboxMessage = new OutboxMessage
                    {
                        Id = integrationEvent.Id,
                        Type = type,
                        Payload = payload,
                        OccurredAt = integrationEvent.OccurredAt,
                        Status = "pending",
                        Attempts = 0,
                        NextAttemptAt = integrationEvent.OccurredAt,
                        ProcessedAt = null,
                        LastError = null,
                    };

                    outboxSet.Add(outboxMessage);
                }

                clearAction();
            }
        }
    }

    private static bool TryGetEvents(object entity, out IReadOnlyCollection<IIntegrationEvent> events, out Action clearAction)
    {
        events = [];
        clearAction = () => { };

        var type = entity.GetType();
        while (type != null && type != typeof(object))
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Entity<>))
            {
                var eventsProp = type.GetProperty(nameof(Entity<object>.IntegrationEvents));
                var clearMethod = type.GetMethod(nameof(Entity<object>.ClearIntegrationEvents));

                if (eventsProp != null && clearMethod != null)
                {
                    var propValue = eventsProp.GetValue(entity);
                    if (propValue is IReadOnlyCollection<IIntegrationEvent> eventList)
                    {
                        events = eventList;
                        clearAction = () => clearMethod.Invoke(entity, null);
                        return true;
                    }
                }
            }

            type = type.BaseType;
        }

        return false;
    }
}
