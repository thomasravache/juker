using FluentAssertions;

namespace Juker.Modules.Catalog.Infrastructure;

/// <summary>ARQ-06: um tipo Juker usa o FluentAssertions.</summary>
public static class SongAssertions
{
    public static bool IsPresent(object? song) => song.Should();
}
