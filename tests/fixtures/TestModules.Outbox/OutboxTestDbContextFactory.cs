using Juker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TestModules.Outbox;

/// <summary>Fábrica de design (dotnet ef migrations add): connection string de placeholder e histórico no schema do módulo.</summary>
public sealed class OutboxTestDbContextFactory : IDesignTimeDbContextFactory<OutboxTestDbContext>
{
    private const string DesignConnectionString = "Host=localhost;Database=outbox_test_design";

    public OutboxTestDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseNpgsql(DesignConnectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", OutboxTestDbContext.SchemaName))
            .Options;
        return new OutboxTestDbContext(options, new FixedTenantContext(null));
    }
}
