using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juker.Modules.Playback;

public static class PlaybackModule
{
    public static IServiceCollection AddPlaybackModule(this IServiceCollection s, IConfiguration c)
        => throw new NotImplementedException();

    public static IEndpointRouteBuilder MapPlaybackEndpoints(this IEndpointRouteBuilder e)
        => throw new NotImplementedException();
}
