namespace Juker.Host;

/// <summary>Compõe os módulos do <see cref="ModuleCatalog"/> no host: único ponto de composição (ADR-0001).</summary>
internal static class ModuleCatalogExtensions
{
    public static IServiceCollection AddJukerModules(this IServiceCollection services, IConfiguration configuration)
    {
        foreach (var module in ModuleCatalog.All)
        {
            module.Add(services, configuration);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapJukerModules(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in ModuleCatalog.All)
        {
            module.Map(endpoints);
        }

        return endpoints;
    }
}
