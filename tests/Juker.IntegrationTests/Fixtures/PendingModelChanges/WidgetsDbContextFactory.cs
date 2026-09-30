using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Juker.IntegrationTests.PendingModelChanges;

/// <summary>Fábrica de design-time: uma das formas pelas quais o guard pode instanciar o contexto.</summary>
public sealed class WidgetsDbContextFactory : IDesignTimeDbContextFactory<WidgetsDbContext>
{
    public WidgetsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<WidgetsDbContext>().UseNpgsql(WidgetsDbContext.ConnectionString).Options);
}
