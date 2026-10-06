using Juker.Infrastructure;
using Juker.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace TestModules.Outbox;

/// <summary>DbContext do módulo de teste (schema <c>outbox_test</c>): uma entidade <c>ITenantScoped</c> e o outbox/inbox da base.</summary>
public sealed class OutboxTestDbContext(DbContextOptions<OutboxTestDbContext> options, ITenantContext tenant)
    : ModuleDbContext(options, tenant, SchemaName)
{
    public const string SchemaName = "outbox_test";

    public DbSet<TestItem> Items => Set<TestItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        var item = modelBuilder.Entity<TestItem>();
        item.HasKey(entity => entity.Id);
        item.Property(entity => entity.VenueId).HasConversion(venue => venue.Value, value => new VenueId(value));
        item.Property(entity => entity.Name).HasMaxLength(200);
        item.Ignore(entity => entity.IntegrationEvents);
    }
}
