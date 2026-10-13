using AwesomeAssertions;
using Juker.Infrastructure;
using Juker.SharedKernel;
using Juker.TestKit;
using Juker.TestKit.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TestModules.Outbox;
using Xunit;

namespace Juker.IntegrationTests.BuildingBlocks;

[Trait("Category", "Integration")]
public sealed class OutboxDrainTests(PostgresFixture db)
{
    [Fact]
    [Trait("Spec", "SPEC-0004:IT-05")]
    public async Task Host_real_monta_o_kit_e_nao_registra_o_dispatcher_desligado()
    {
        // Dado o host real (Juker.Host com AddJukerInfrastructure) com Outbox:DispatcherEnabled=false
        await using var factory = new JukerApiFactory(db);
        var services = factory.Services;

        // Então o drainer, o gerador de ids (Guid v7) e o TimeProvider resolvem
        services.GetRequiredService<IOutboxDrainer>().Should().NotBeNull();
        services.GetRequiredService<IIdGenerator>().NewId().Version.Should().Be(7);
        services.GetRequiredService<TimeProvider>().Should().BeSameAs(factory.Time);
        services.GetRequiredService<IOptions<OutboxOptions>>().Value.DispatcherEnabled.Should().BeFalse();

        // E o OutboxDispatcher não está entre os hosted services
        services.GetServices<IHostedService>().Select(service => service.GetType().Name).Should().NotContain("OutboxDispatcher");
    }

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-05")]
    public async Task DrainOutboxAsync_entrega_as_pendentes_sob_comando_e_depois_retorna_zero()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dado o host real com o módulo de teste registrado pelo ponto de extensão de serviços da factory
        await using var factory = new JukerApiFactory(db);
        var probe = new ConsumerProbe(factory.Time);
        var venue = new VenueId(Guid.NewGuid());
        factory.UseTestIssuers(services =>
        {
            services.AddSingleton(probe);
            services.RemoveAll<ITenantContext>();
            services.AddSingleton<ITenantContext>(new FixedTenantContext(venue));
            services.AddModuleDbContext<OutboxTestDbContext>();
            services.AddModuleHandlers(typeof(OutboxTestDbContext).Assembly);
        });
        var connectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("juker")!;

        // E 3 mensagens pendentes no outbox do módulo de teste (migration real aplicada no banco da factory)
        var events = Enumerable.Range(1, 3).Select(sequence => new DrainEvent(Guid.NewGuid(), factory.Time.GetUtcNow(), sequence)).ToList();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
            await context.Database.MigrateAsync(ct);
            var carrier = TestItem.Carrier(Guid.NewGuid(), venue);
            events.ForEach(carrier.Publish);
            context.Items.Add(carrier);
            await context.SaveChangesAsync(ct);
        }

        // Quando o relógio avança 2 s, então nada é entregue sozinho
        factory.Time.Advance(TimeSpan.FromSeconds(2));
        probe.Deliveries.Should().BeEmpty();
        (await OutboxTables.OutboxAsync(connectionString, ct)).Should().HaveCount(3).And.OnlyContain(row => row.Status == "pending");

        // Quando DrainOutboxAsync é chamado, então entrega as 3 na hora e retorna 3
        (await factory.DrainOutboxAsync()).Should().Be(3);
        probe.Of<DrainConsumer>().Select(delivery => delivery.EventId).Should().BeEquivalentTo(events.Select(e => e.Id));
        probe.Deliveries.Should().OnlyContain(delivery => delivery.Succeeded);
        (await OutboxTables.OutboxAsync(connectionString, ct)).Should().OnlyContain(row => row.Status == "processed");

        // E a chamada seguinte retorna 0, sem nova entrega
        (await factory.DrainOutboxAsync()).Should().Be(0);
        probe.Deliveries.Should().HaveCount(3);
    }
}
