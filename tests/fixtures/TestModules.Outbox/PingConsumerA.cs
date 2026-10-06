using Juker.Infrastructure;

namespace TestModules.Outbox;

/// <summary>Primeiro consumidor de PingEvent (IT-02); falha quando configurado no ConsumerProbe.</summary>
public sealed class PingConsumerA(ConsumerProbe probe) : IIntegrationEventHandler<PingEvent>
{
    public Task Handle(PingEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        probe.Record<PingConsumerA>(integrationEvent.Id);
        return Task.CompletedTask;
    }
}
