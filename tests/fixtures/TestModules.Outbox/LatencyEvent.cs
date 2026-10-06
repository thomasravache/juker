using Juker.SharedKernel;

namespace TestModules.Outbox;

/// <summary>Evento de integração de teste. Consumido por LatencyConsumer (IT-06).</summary>
public sealed record LatencyEvent(Guid Id, DateTimeOffset OccurredAt, int Sequence) : IIntegrationEvent;
