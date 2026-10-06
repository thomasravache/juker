using Juker.SharedKernel;

namespace TestModules.Outbox;

/// <summary>Evento de integração de teste. Consumido por HealthyConsumer, que sempre funciona (IT-03).</summary>
public sealed record HealthyEvent(Guid Id, DateTimeOffset OccurredAt, int Sequence) : IIntegrationEvent;
