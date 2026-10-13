using Microsoft.EntityFrameworkCore;

namespace Juker.IntegrationTests.CleanMigration;

/// <summary>DbContext de fixture com uma migration (CreateGadgets) e sem mudanças pendentes no modelo.</summary>
public sealed class GadgetsDbContext(DbContextOptions<GadgetsDbContext> options) : DbContext(options)
{
    public const string Schema = "clean_migration";

    public const string Table = "gadgets";

    public DbSet<Gadget> Gadgets => Set<Gadget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Gadget>(gadget =>
        {
            gadget.ToTable(Table);
            gadget.HasKey(g => g.Id);
            gadget.Property(g => g.Id).ValueGeneratedNever();
        });
    }
}
