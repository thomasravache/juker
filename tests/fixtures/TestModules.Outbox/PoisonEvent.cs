using Juker.SharedKernel;

namespace TestModules.Outbox;

/// <summary>Evento de integração de teste. Consumido por PoisonConsumer, que sempre falha (IT-03).</summary>
public sealed record PoisonEvent(Guid Id, DateTimeOffset OccurredAt, int Sequence) : IIntegrationEvent;
