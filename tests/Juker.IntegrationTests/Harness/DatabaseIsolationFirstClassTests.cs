using Juker.TestKit;
using Xunit;

namespace Juker.IntegrationTests.Harness;

[Trait("Category", "Integration")]
public sealed class DatabaseIsolationFirstClassTests(PostgresFixture db)
{
    [Fact]
    [Trait("Spec", "SPEC-0003:IT-01")]
    public async Task Banco_da_primeira_classe_isola_a_mesma_chave_e_tem_pg_trgm_e_unaccent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado um banco da PostgresFixture para esta classe
        var connectionString = await db.CreateDatabaseAsync();

        // Quando grava a mesma chave primária que a outra classe grava, então só enxerga o próprio dado
        await IsolationProbe.WriteAndReadOwnRowAsync(connectionString, "primeira", cancellationToken);

        // E as extensões pg_trgm e unaccent estão disponíveis
        await IsolationProbe.ExtensionsShouldWorkAsync(connectionString, cancellationToken);
    }
}
