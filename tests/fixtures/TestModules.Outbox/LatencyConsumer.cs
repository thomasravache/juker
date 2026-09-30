using Juker.Infrastructure;

namespace TestModules.Outbox;

/// <summary>Consumidor do IT-06 (latência gravação → entrega).</summary>
public sealed class LatencyConsumer(ConsumerProbe probe) : IIntegrationEventHandler<LatencyEvent>
{
    public Task Handle(LatencyEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        probe.Record<LatencyConsumer>(integrationEvent.Id);
        return Task.CompletedTask;
    }
}
