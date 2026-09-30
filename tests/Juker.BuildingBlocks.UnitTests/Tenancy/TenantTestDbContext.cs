using Juker.Infrastructure;
using Juker.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Juker.BuildingBlocks.UnitTests.Tenancy;

/// <summary><see cref="ModuleDbContext"/> de teste do UT-04 (schema <c>unit_test</c>), sem banco real.</summary>
public sealed class TenantTestDbContext(DbContextOptions<TenantTestDbContext> options, ITenantContext tenant)
    : ModuleDbContext(options, tenant, "unit_test")
{
    public DbSet<TenantItem> Items => Set<TenantItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        var item = modelBuilder.Entity<TenantItem>();
        item.HasKey(entity => entity.Id);
        item.Property(entity => entity.VenueId).HasConversion(venue => venue.Value, value => new VenueId(value));
        item.Ignore(entity => entity.IntegrationEvents);
    }
}
