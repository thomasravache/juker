using Juker.SharedKernel;

namespace TestModules.Outbox;

/// <summary>Evento de integração de teste. Consumido por DrainConsumer (IT-05).</summary>
public sealed record DrainEvent(Guid Id, DateTimeOffset OccurredAt, int Sequence) : IIntegrationEvent;
