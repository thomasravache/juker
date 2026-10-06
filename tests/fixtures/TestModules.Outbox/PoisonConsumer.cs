using Juker.Infrastructure;

namespace TestModules.Outbox;

/// <summary>Consumidor que sempre lança exceção (IT-03): a mensagem termina <c>failed</c> após MaxAttempts.</summary>
public sealed class PoisonConsumer(ConsumerProbe probe) : IIntegrationEventHandler<PoisonEvent>
{
    public Task Handle(PoisonEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        probe.Record<PoisonConsumer>(integrationEvent.Id, forceFailure: true);
        return Task.CompletedTask;
    }
}
