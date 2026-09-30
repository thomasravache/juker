namespace Juker.Modules.Catalog.Infrastructure.YouTube;

/// <summary>Controle negativo do ARQ-07: Catalog.Infrastructure pode conhecer o YouTube.</summary>
public sealed class YouTubeSearchClient
{
    public Uri Endpoint { get; init; } = new("https://www.googleapis.com/youtube/v3/", UriKind.Absolute);
}
