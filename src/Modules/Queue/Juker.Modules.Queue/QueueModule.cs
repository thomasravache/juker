using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juker.Modules.Queue;

public static class QueueModule
{
    public static IServiceCollection AddQueueModule(this IServiceCollection s, IConfiguration c)
        => throw new NotImplementedException();

    public static IEndpointRouteBuilder MapQueueEndpoints(this IEndpointRouteBuilder e)
        => throw new NotImplementedException();
}
