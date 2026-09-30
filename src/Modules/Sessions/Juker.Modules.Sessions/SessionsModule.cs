using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juker.Modules.Sessions;

public static class SessionsModule
{
    public static IServiceCollection AddSessionsModule(this IServiceCollection s, IConfiguration c)
        => throw new NotImplementedException();

    public static IEndpointRouteBuilder MapSessionsEndpoints(this IEndpointRouteBuilder e)
        => throw new NotImplementedException();
}
