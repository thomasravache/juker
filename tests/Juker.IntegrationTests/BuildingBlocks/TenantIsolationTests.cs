using AwesomeAssertions;
using Juker.SharedKernel;
using Juker.TestKit;
using Microsoft.EntityFrameworkCore;
using TestModules.Outbox;
using Xunit;

namespace Juker.IntegrationTests.BuildingBlocks;

[Trait("Category", "Integration")]
public sealed class TenantIsolationTests(PostgresFixture db)
{
    [Fact]
    [Trait("Spec", "SPEC-0004:IT-04")]
    public async Task Contexto_do_bar_A_retorna_somente_as_linhas_do_A()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dado linhas de dois bares numa tabela ITenantScoped
        var connectionString = await db.CreateDatabaseAsync();
        var time = TestClock.New();
        await using var host = await OutboxTestHost.StartAsync(
            connectionString, new OutboxTestHostOptions(new ConsumerProbe(time), DispatcherEnabled: false, time), ct);
        var barA = new VenueId(Guid.NewGuid());
        var barB = new VenueId(Guid.NewGuid());
        await SeedAsync(host, barA, ["A1", "A2"], time, ct);
        await SeedAsync(host, barB, ["B1"], time, ct);

        // Quando o contexto do bar A consulta a tabela
        await using var contextA = host.CreateContext(barA);
        var itemsA = await contextA.Items.AsNoTracking().ToListAsync(ct);

        // Então só as linhas do A retornam
        itemsA.Select(item => item.Name).Should().BeEquivalentTo("A1", "A2");
        itemsA.Should().OnlyContain(item => item.VenueId == barA);

        // E o contexto do B vê só as do B, embora as três linhas existam
        await using var contextB = host.CreateContext(barB);
        (await contextB.Items.AsNoTracking().Select(item => item.Name).ToListAsync(ct)).Should().Equal("B1");
        (await contextA.Items.IgnoreQueryFilters().CountAsync(ct)).Should().Be(3);
    }

    private static async Task SeedAsync(OutboxTestHost host, VenueId venue, string[] names, TimeProvider time, CancellationToken ct)
    {
        await using var context = host.CreateContext(venue);
        foreach (var name in names)
        {
            context.Items.Add(TestItem.Create(Guid.NewGuid(), venue, name, Guid.NewGuid(), time.GetUtcNow()));
        }

        await context.SaveChangesAsync(ct);
    }
}
