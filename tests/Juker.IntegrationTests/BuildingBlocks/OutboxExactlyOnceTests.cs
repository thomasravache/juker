using AwesomeAssertions;
using Juker.Infrastructure;
using Juker.SharedKernel;
using Juker.TestKit;
using Microsoft.Extensions.DependencyInjection;
using TestModules.Outbox;
using Xunit;

namespace Juker.IntegrationTests.BuildingBlocks;

[Trait("Category", "Integration")]
public sealed class OutboxExactlyOnceTests(PostgresFixture db)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-02")]
    public async Task Cada_consumidor_executa_exatamente_uma_vez_com_falha_reprocessamento_e_host_religado()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dado dois consumidores do mesmo evento, com A configurado para falhar na primeira entrega
        var connectionString = await db.CreateDatabaseAsync();
        var time = TestClock.New();
        var probe = new ConsumerProbe(time);
        probe.FailNext<PingConsumerA>(1);
        var venue = new VenueId(Guid.NewGuid());
        var options = new OutboxTestHostOptions(probe, DispatcherEnabled: true, time, venue);
        var ping = new PingEvent(Guid.NewGuid(), time.GetUtcNow(), 1);

        // Quando o dispatcher faz a primeira entrega e o host é parado em seguida
        var message = await FirstDeliveryThenStopAsync(connectionString, options, ping, ct);

        // Então A falhou, B entregou e a mensagem continua pending para reprocessamento
        message.Status.Should().Be("pending");
        probe.Of<PingConsumerA>().Select(delivery => delivery.Succeeded).Should().Equal(false);
        probe.Of<PingConsumerB>().Select(delivery => delivery.Succeeded).Should().Equal(true);

        // Quando o host é religado sobre o mesmo banco e o backoff passa
        await using var second = await OutboxTestHost.StartAsync(connectionString, options, ct);
        await Eventually.UntilAsync(
            "mensagem reprocessada e marcada processed",
            async () => string.Equals(
                (await OutboxTables.OutboxMessageAsync(connectionString, message.Id, ct)).Status, "processed", StringComparison.Ordinal),
            Timeout,
            ct,
            onTick: () => time.Advance(TestClock.DispatcherInterval),
            tickEvery: TimeSpan.FromMilliseconds(200));

        // Então A executou com sucesso exatamente uma vez (depois da falha) e B continua com uma única execução
        probe.Of<PingConsumerA>().Select(delivery => delivery.Succeeded).Should().Equal(false, true);
        probe.Of<PingConsumerB>().Select(delivery => delivery.Succeeded).Should().Equal(true);
        probe.Deliveries.Should().OnlyContain(delivery => delivery.EventId == ping.Id);

        // E o inbox tem uma linha por consumidor (nome completo do tipo do handler)
        var inbox = await OutboxTables.InboxAsync(connectionString, ct);
        inbox.Should().OnlyContain(row => row.MessageId == message.Id);
        inbox.Select(row => row.Consumer).Should().BeEquivalentTo(typeof(PingConsumerA).FullName, typeof(PingConsumerB).FullName);

        // E nenhuma execução se repete: não sobra nada para entregar
        (await second.Services.GetRequiredService<IOutboxDrainer>().DrainAsync(ct)).Should().Be(0);
        probe.Deliveries.Should().HaveCount(3);
    }

    private static async Task<OutboxRow> FirstDeliveryThenStopAsync(
        string connectionString,
        OutboxTestHostOptions options,
        PingEvent ping,
        CancellationToken ct)
    {
        var time = (Microsoft.Extensions.Time.Testing.FakeTimeProvider)options.Time!;
        await using var first = await OutboxTestHost.StartAsync(connectionString, options, ct);
        first.HostedServiceNames().Should().Contain("OutboxDispatcher", "o dispatcher é registrado com DispatcherEnabled=true");
        await first.PublishAsync(options.Tenant!.Value, [ping], ct);

        // O relógio só avança até a primeira entrega acontecer: o retry (1 s depois) não chega antes da parada
        await Eventually.UntilAsync(
            "primeira entrega registrada no outbox",
            async () => (await OutboxTables.OutboxAsync(connectionString, ct)).Single().Attempts == 1,
            Timeout,
            ct,
            onTick: () =>
            {
                if (options.Probe.Deliveries.Count == 0)
                {
                    time.Advance(TestClock.DispatcherInterval);
                }
            });

        var message = (await OutboxTables.OutboxAsync(connectionString, ct)).Single();
        await first.StopAsync(ct);
        return message;
    }
}
