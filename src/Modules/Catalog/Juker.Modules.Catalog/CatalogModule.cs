using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juker.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection s, IConfiguration c)
        => throw new NotImplementedException();

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder e)
        => throw new NotImplementedException();
}
