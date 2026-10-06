using System.Text.Json;
using AwesomeAssertions;
using Juker.Infrastructure;
using Juker.SharedKernel;
using Juker.TestKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestModules.Outbox;
using Xunit;

namespace Juker.IntegrationTests.BuildingBlocks;

[Trait("Category", "Integration")]
public sealed class OutboxTransactionTests(PostgresFixture db)
{
    private const string Pending = "pending";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-01")]
    public async Task Handler_com_commit_grava_a_entidade_e_a_mensagem_pending_no_outbox()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dado o kit montado sobre o módulo de teste, com o dispatcher desligado
        var connectionString = await db.CreateDatabaseAsync();
        var time = TestClock.New();
        var venue = new VenueId(Guid.NewGuid());
        await using var host = await OutboxTestHost.StartAsync(
            connectionString, new OutboxTestHostOptions(new ConsumerProbe(time), DispatcherEnabled: false, time, venue), ct);
        var command = new CreateTestItem(Guid.NewGuid(), venue, "Balcão", Guid.NewGuid(), time.GetUtcNow());

        // Quando o handler de comando (com os decorators) grava a entidade que levanta o evento
        Result<Guid> result;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateTestItem, Guid>>();
            result = await handler.Handle(command, ct);
        }

        // Então a entidade existe
        result.IsSuccess.Should().BeTrue();
        await using (var context = host.CreateContext(venue))
        {
            (await context.Items.CountAsync(item => item.Id == command.ItemId, ct)).Should().Be(1);
        }

        // E há uma mensagem pending no outbox com type e payload do contrato
        var message = (await OutboxTables.OutboxAsync(connectionString, ct)).Should().ContainSingle().Subject;
        message.Status.Should().Be(Pending);
        message.ProcessedAt.Should().BeNull();
        message.LastError.Should().BeNull();
        message.Type.Should().Be($"{typeof(TestItemCreated).FullName}, {typeof(TestItemCreated).Assembly.GetName().Name}");

        var expected = new TestItemCreated(command.EventId, command.OccurredAt, command.ItemId, venue, command.Name);
        JsonSerializer.Deserialize<TestItemCreated>(message.Payload, WebJson)
            .Should().Be(expected);
        using var payload = JsonDocument.Parse(message.Payload);
        payload.RootElement.TryGetProperty("itemId", out _).Should().BeTrue("o payload usa JsonSerializerDefaults.Web (camelCase)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-01")]
    public async Task Transacao_revertida_nao_deixa_a_entidade_nem_a_mensagem()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dado o kit montado sobre o módulo de teste, com o dispatcher desligado
        var connectionString = await db.CreateDatabaseAsync();
        var time = TestClock.New();
        var venue = new VenueId(Guid.NewGuid());
        await using var host = await OutboxTestHost.StartAsync(
            connectionString, new OutboxTestHostOptions(new ConsumerProbe(time), DispatcherEnabled: false, time, venue), ct);
        var itemId = Guid.NewGuid();

        // Quando a entidade que levanta o evento é gravada numa transação que depois é revertida
        await using (var context = host.CreateContext(venue))
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            context.Items.Add(TestItem.Create(itemId, venue, "Balcão", Guid.NewGuid(), time.GetUtcNow()));
            await context.SaveChangesAsync(ct);

            // (a mensagem foi gravada pelo interceptor dentro da mesma transação)
            var insideTransaction = await context.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM outbox_test.outbox_messages")
                .SingleAsync(ct);
            insideTransaction.Should().Be(1);

            await transaction.RollbackAsync(ct);
        }

        // Então nem a entidade nem a mensagem existem
        await using (var context = host.CreateContext(venue))
        {
            (await context.Items.CountAsync(item => item.Id == itemId, ct)).Should().Be(0);
        }

        (await OutboxTables.OutboxAsync(connectionString, ct)).Should().BeEmpty();
    }
}
