using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Juker.IntegrationTests.CleanMigration;

/// <summary>
/// Fábrica de design-time (o jeito recomendado no docs/testing.md): connection string de placeholder, que a
/// PostgresFixture troca pela do juker_template, e o histórico das migrations no schema do contexto.
/// </summary>
public sealed class GadgetsDbContextFactory : IDesignTimeDbContextFactory<GadgetsDbContext>
{
    private const string DesignConnectionString = "Host=localhost;Database=clean_migration_design";

    public GadgetsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<GadgetsDbContext>()
            .UseNpgsql(DesignConnectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", GadgetsDbContext.Schema))
            .Options);
}
