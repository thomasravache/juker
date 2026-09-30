using System.Diagnostics;
using AwesomeAssertions;
using Juker.SharedKernel;
using Juker.TestKit;
using TestModules.Outbox;
using Xunit;

namespace Juker.IntegrationTests.BuildingBlocks;

[Trait("Category", "Integration")]
public sealed class OutboxLatencyTests(PostgresFixture db)
{
    private const int Messages = 100;
    private static readonly TimeSpan P95Budget = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-06")]
    public async Task P95_entre_gravacao_e_entrega_fica_abaixo_de_1_s_com_100_pendentes()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dado o dispatcher ligado com o intervalo padrão (500 ms) e o relógio real
        var connectionString = await db.CreateDatabaseAsync();
        var probe = new ConsumerProbe(TimeProvider.System);
        var venue = new VenueId(Guid.NewGuid());
        await using var host = await OutboxTestHost.StartAsync(
            connectionString, new OutboxTestHostOptions(probe, DispatcherEnabled: true, Tenant: venue, IntervalMs: 500), ct);
        host.HostedServiceNames().Should().Contain("OutboxDispatcher");

        // Quando 100 mensagens são gravadas
        var events = Enumerable.Range(1, Messages)
            .Select(sequence => new LatencyEvent(Guid.NewGuid(), TimeProvider.System.GetUtcNow(), sequence))
            .ToList();
        await host.PublishAsync(venue, events, ct);
        var written = Stopwatch.GetTimestamp();

        // Então todas são entregues (espera por condição) e o p95 gravação → entrega fica abaixo de 1 s
        await probe.WaitUntilAsync(
            deliveries => deliveries.Count(delivery => string.Equals(delivery.Consumer, typeof(LatencyConsumer).FullName, StringComparison.Ordinal)) >= Messages,
            TimeSpan.FromSeconds(30),
            ct);
        var deliveries = probe.Of<LatencyConsumer>();
        deliveries.Select(delivery => delivery.EventId).Should().BeEquivalentTo(events.Select(e => e.Id));

        var latencies = deliveries.Select(delivery => Stopwatch.GetElapsedTime(written, delivery.Timestamp)).Order().ToList();
        var p95 = latencies[(int)Math.Ceiling(0.95 * latencies.Count) - 1];
        p95.Should().BeLessThan(P95Budget, "latência p95 medida: {0:F0} ms", p95.TotalMilliseconds);
    }
}
