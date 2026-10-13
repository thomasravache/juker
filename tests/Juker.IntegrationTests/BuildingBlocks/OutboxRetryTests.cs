using AwesomeAssertions;
using Juker.Infrastructure;
using Juker.SharedKernel;
using Juker.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TestModules.Outbox;
using Xunit;

namespace Juker.IntegrationTests.BuildingBlocks;

[Trait("Category", "Integration")]
public sealed class OutboxRetryTests(PostgresFixture db)
{
    private const int MaxAttempts = 5;
    private const string Pending = "pending";
    private const string Processed = "processed";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Sem progresso por este tempo real, o relógio avança mais um intervalo do dispatcher.
    private static readonly TimeSpan NudgeEvery = TimeSpan.FromMilliseconds(500);

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-03")]
    public async Task Consumidor_que_sempre_falha_leva_a_mensagem_a_failed_apos_5_tentativas_com_backoff()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dado uma mensagem cujo consumidor sempre falha e outra mensagem saudável, com o dispatcher ligado
        var connectionString = await db.CreateDatabaseAsync();
        var time = TestClock.New();
        var probe = new ConsumerProbe(time);
        var venue = new VenueId(Guid.NewGuid());
        await using var host = await OutboxTestHost.StartAsync(
            connectionString, new OutboxTestHostOptions(probe, DispatcherEnabled: true, time, venue), ct);
        await host.PublishAsync(
            venue,
            [new PoisonEvent(Guid.NewGuid(), time.GetUtcNow(), 1), new HealthyEvent(Guid.NewGuid(), time.GetUtcNow(), 2)],
            ct);
        var rows = await OutboxTables.OutboxAsync(connectionString, ct);
        var poisonId = rows.Single(row => row.Type.StartsWith(typeof(PoisonEvent).FullName + ",", StringComparison.Ordinal)).Id;
        var healthyId = rows.Single(row => row.Type.StartsWith(typeof(HealthyEvent).FullName + ",", StringComparison.Ordinal)).Id;

        // Quando o dispatcher faz a primeira entrega das duas
        await FirstDeliveryAsync(poisonId, healthyId, time, probe, Row, ct);

        // E o relógio avança até cada próxima tentativa agendada (next_attempt_at)
        await RetryUntilExhaustedAsync(poisonId, time, probe, Row, ct);

        // Então, após 5 tentativas, a mensagem fica failed com o erro registrado
        var failed = await Row(poisonId);
        failed.Status.Should().Be("failed");
        failed.Attempts.Should().Be(MaxAttempts);
        failed.ProcessedAt.Should().BeNull();
        failed.LastError.Should().NotBeNullOrWhiteSpace().And.Contain("Falha configurada");

        // E nenhuma tentativa aconteceu antes do backoff (1, 2, 4, 8 s entre as execuções)
        var attempts = probe.Of<PoisonConsumer>();
        attempts.Should().HaveCount(MaxAttempts).And.OnlyContain(delivery => !delivery.Succeeded);
        for (var retry = 1; retry < MaxAttempts; retry++)
        {
            (attempts[retry].At - attempts[retry - 1].At).Should().BeGreaterThanOrEqualTo(Backoff(retry));
        }

        // E a outra mensagem foi entregue normalmente, uma vez
        probe.Of<HealthyConsumer>().Select(delivery => delivery.Succeeded).Should().Equal(true);
        var healthy = await Row(healthyId);
        healthy.Status.Should().Be(Processed);
        healthy.ProcessedAt.Should().NotBeNull();

        // E a mensagem failed não é mais entregue
        (await host.Services.GetRequiredService<IOutboxDrainer>().DrainAsync(ct)).Should().Be(0);
        probe.Of<PoisonConsumer>().Should().HaveCount(MaxAttempts);

        Task<OutboxRow> Row(Guid id) => OutboxTables.OutboxMessageAsync(connectionString, id, ct);
    }

    private static Task FirstDeliveryAsync(
        Guid poisonId,
        Guid healthyId,
        FakeTimeProvider time,
        ConsumerProbe probe,
        Func<Guid, Task<OutboxRow>> row,
        CancellationToken ct) =>
        Eventually.UntilAsync(
            "primeira tentativa da mensagem que falha e entrega da saudável",
            async () => (await row(poisonId)).Attempts == 1
                && string.Equals((await row(healthyId)).Status, Processed, StringComparison.Ordinal),
            Timeout,
            ct,
            onTick: () =>
            {
                // Só até as duas primeiras execuções: depois disso o relógio para, e o retry não é antecipado.
                if (probe.Of<PoisonConsumer>().Count == 0 || probe.Of<HealthyConsumer>().Count == 0)
                {
                    time.Advance(TestClock.DispatcherInterval);
                }
            });

    // next_attempt_at = agora + 2^(attempts-1) s: 1, 2, 4 e 8 s depois das tentativas 1 a 4.
    private static TimeSpan Backoff(int attempts) => TimeSpan.FromSeconds(Math.Pow(2, attempts - 1));

    private static async Task RetryUntilExhaustedAsync(
        Guid messageId,
        FakeTimeProvider time,
        ConsumerProbe probe,
        Func<Guid, Task<OutboxRow>> row,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt < MaxAttempts; attempt++)
        {
            var current = await row(messageId);
            current.Status.Should().Be(Pending, "a tentativa {0} de {1} falhou, mas ainda há tentativas", attempt, MaxAttempts);
            current.Attempts.Should().Be(attempt);

            var failedAt = probe.Of<PoisonConsumer>()[attempt - 1].At;
            (current.NextAttemptAt - failedAt).Should().BeGreaterThanOrEqualTo(Backoff(attempt))
                .And.BeLessThanOrEqualTo(Backoff(attempt) + TestClock.DispatcherInterval);

            var untilNext = current.NextAttemptAt - time.GetUtcNow();
            if (untilNext > TimeSpan.Zero)
            {
                time.Advance(untilNext);
            }

            var expected = attempt + 1;
            await Eventually.UntilAsync(
                $"tentativa {expected} registrada",
                async () => (await row(messageId)).Attempts == expected,
                Timeout,
                ct,
                onTick: () => time.Advance(TestClock.DispatcherInterval),
                tickEvery: NudgeEvery);
        }
    }
}
