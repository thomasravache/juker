using Juker.SharedKernel;
using TestModules.Outbox;

namespace Juker.IntegrationTests.BuildingBlocks;

/// <summary>Como montar o <see cref="OutboxTestHost"/>: dispatcher, relógio, tenant e o probe dos consumidores.</summary>
/// <param name="Probe">Registro das execuções dos consumidores de teste (singleton do host).</param>
/// <param name="DispatcherEnabled">Valor de <c>Outbox:DispatcherEnabled</c>.</param>
/// <param name="Time">Relógio do host; <c>null</c> mantém o padrão do kit (<c>TimeProvider.System</c>).</param>
/// <param name="Tenant">Bar do <c>ITenantContext</c> do host.</param>
/// <param name="IntervalMs">Valor de <c>Outbox:IntervalMs</c>.</param>
internal sealed record OutboxTestHostOptions(
    ConsumerProbe Probe,
    bool DispatcherEnabled,
    TimeProvider? Time = null,
    VenueId? Tenant = null,
    int IntervalMs = 500);
