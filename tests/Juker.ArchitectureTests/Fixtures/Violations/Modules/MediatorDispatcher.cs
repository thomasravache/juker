namespace Juker.Infrastructure.Messaging;

/// <summary>
/// Controle negativo do ARQ-06: os handlers próprios do Juker (ADR-0002) podem se chamar "Mediator" sem depender
/// do MediatR, MassTransit nem FluentAssertions.
/// </summary>
public static class MediatorDispatcher
{
    public static Task DispatchAsync(object command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Task.CompletedTask;
    }
}
