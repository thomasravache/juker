namespace MassTransit;

/// <summary>Simula o MassTransit (ARQ-06) sem referenciar o pacote comercial.</summary>
public interface IBus
{
    Task Publish(object message, CancellationToken cancellationToken);
}
