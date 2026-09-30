using MassTransit;

namespace Juker.Modules.Queue.Infrastructure;

/// <summary>ARQ-06: um tipo Juker depende do MassTransit.</summary>
public sealed class QueueEventPublisher(IBus bus)
{
    public Task PublishAsync(object message, CancellationToken cancellationToken) => bus.Publish(message, cancellationToken);
}
