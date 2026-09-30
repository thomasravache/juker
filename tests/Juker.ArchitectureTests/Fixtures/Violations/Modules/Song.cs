using Microsoft.EntityFrameworkCore;

namespace Juker.Modules.Catalog.Domain;

/// <summary>ARQ-04: um tipo de Domain/ depende do EF Core.</summary>
public sealed class Song
{
    public Guid Id { get; init; }

    public static void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<Song>();
    }
}
