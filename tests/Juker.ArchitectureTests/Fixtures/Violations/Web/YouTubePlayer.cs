namespace Juker.Web.Client.Tv.YouTube;

/// <summary>Controle negativo do ARQ-07: o player da TV pode conhecer o YouTube.</summary>
public sealed class YouTubePlayer
{
    public string VideoId { get; init; } = string.Empty;
}
