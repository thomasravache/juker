using Juker.Infrastructure;

namespace TestModules.Outbox;

/// <summary>Consumidor que funciona (IT-03).</summary>
public sealed class HealthyConsumer(ConsumerProbe probe) : IIntegrationEventHandler<HealthyEvent>
{
    public Task Handle(HealthyEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        probe.Record<HealthyConsumer>(integrationEvent.Id);
        return Task.CompletedTask;
    }
}
