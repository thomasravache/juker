using AwesomeAssertions;
using Juker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Juker.BuildingBlocks.UnitTests.Tenancy;

[Trait("Category", "Unit")]
public sealed class TenantFilterTests
{
    // Placeholder: nenhum servidor responde neste host; o teste prova que nenhuma conexão é tentada.
    private const string PlaceholderConnectionString = "Host=juker-placeholder.invalid;Database=juker_unit;Timeout=1";

    [Fact]
    [Trait("Spec", "SPEC-0004:UT-04")]
    public async Task Consulta_a_entidade_ITenantScoped_sem_tenant_lanca_TenantNotResolvedException_sem_abrir_conexao()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dado um ModuleDbContext com Npgsql (placeholder) e FixedTenantContext(null)
        var probe = new ConnectionOpeningProbe();
        var options = new DbContextOptionsBuilder<TenantTestDbContext>()
            .UseNpgsql(PlaceholderConnectionString)
            .AddInterceptors(probe)
            .Options;
        await using var context = new TenantTestDbContext(options, new FixedTenantContext(null));

        // Quando uma entidade ITenantScoped é consultada
        var query = async () => await context.Items.ToListAsync(ct);

        // Então lança TenantNotResolvedException antes de qualquer conexão ser aberta
        await query.Should().ThrowAsync<TenantNotResolvedException>();
        probe.Openings.Should().Be(0);
    }
}
