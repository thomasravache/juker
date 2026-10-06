using Juker.SharedKernel;

namespace TestModules.Outbox;

/// <summary>Evento de integração de teste. Consumido por PingConsumerA e PingConsumerB (IT-02).</summary>
public sealed record PingEvent(Guid Id, DateTimeOffset OccurredAt, int Sequence) : IIntegrationEvent;
