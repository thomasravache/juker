using Microsoft.EntityFrameworkCore;

namespace Juker.IntegrationTests.PendingModelChanges;

public sealed class WidgetsDbContext : DbContext
{
    public const string Schema = "fixture";

    // Sem conexão real: o guard só compara o modelo com o snapshot das migrations.
    public const string ConnectionString = "Host=localhost;Database=pending_model_changes_fixture";

    public WidgetsDbContext()
    {
    }

    public WidgetsDbContext(DbContextOptions<WidgetsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(ConnectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Widget>(widget =>
        {
            widget.ToTable("widgets");
            widget.HasKey(w => w.Id);
            widget.Property(w => w.Id).ValueGeneratedNever();
        });
    }
}
