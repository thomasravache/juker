using Juker.Infrastructure;

namespace TestModules.Outbox;

/// <summary>Consumidor do IT-05.</summary>
public sealed class DrainConsumer(ConsumerProbe probe) : IIntegrationEventHandler<DrainEvent>
{
    public Task Handle(DrainEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        probe.Record<DrainConsumer>(integrationEvent.Id);
        return Task.CompletedTask;
    }
}
