using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juker.Modules.Venues;

public static class VenuesModule
{
    public static IServiceCollection AddVenuesModule(this IServiceCollection s, IConfiguration c)
        => throw new NotImplementedException();

    public static IEndpointRouteBuilder MapVenuesEndpoints(this IEndpointRouteBuilder e)
        => throw new NotImplementedException();
}
