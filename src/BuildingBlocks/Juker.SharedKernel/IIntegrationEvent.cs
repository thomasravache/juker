namespace Juker.SharedKernel;

/// <summary>Evento de integração publicado pelo outbox (ADR-0006); os eventos vivem nos <c>.Contracts</c> (ARQ-02).</summary>
public interface IIntegrationEvent
{
    Guid Id { get; }

    DateTimeOffset OccurredAt { get; }
}
