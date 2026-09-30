namespace Juker.Modules.Queue.YouTube;

/// <summary>ARQ-07: tipo com YouTube no namespace fora de Catalog.Infrastructure e Web.Client.Tv.</summary>
public sealed class YouTubeVideoLookup
{
    public string VideoId { get; init; } = string.Empty;
}
