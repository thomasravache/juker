namespace TestModules.Outbox;

/// <summary>Uma execução de consumidor registrada pelo <see cref="ConsumerProbe"/>.</summary>
/// <param name="Consumer">Nome completo do tipo do consumidor (o mesmo do inbox).</param>
/// <param name="EventId">Id do evento entregue.</param>
/// <param name="Succeeded">Se a execução terminou sem exceção.</param>
/// <param name="At">Instante pelo <see cref="TimeProvider"/> do teste.</param>
/// <param name="Timestamp">Marca do <see cref="System.Diagnostics.Stopwatch"/> (relógio real, para latência).</param>
public sealed record Delivery(string Consumer, Guid EventId, bool Succeeded, DateTimeOffset At, long Timestamp);
