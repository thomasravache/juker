using Microsoft.EntityFrameworkCore;

namespace Juker.Infrastructure;

/// <summary>
/// Base do <c>DbContext</c> de cada módulo (ADR-0004, ADR-0008): schema do módulo, snake_case, filtro global de tenant
/// para <c>ITenantScoped</c> (falha fechado) e as entidades de outbox/inbox do módulo.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    protected ModuleDbContext(DbContextOptions options, ITenantContext tenant, string schema)
        : base(options) => throw new NotImplementedException();

    protected string Schema => throw new NotImplementedException();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => throw new NotImplementedException();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) => throw new NotImplementedException();
}
