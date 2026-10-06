using Juker.Infrastructure;

namespace TestModules.Outbox;

/// <summary>Segundo consumidor de PingEvent (IT-02).</summary>
public sealed class PingConsumerB(ConsumerProbe probe) : IIntegrationEventHandler<PingEvent>
{
    public Task Handle(PingEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        probe.Record<PingConsumerB>(integrationEvent.Id);
        return Task.CompletedTask;
    }
}
