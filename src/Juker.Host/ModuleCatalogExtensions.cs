using Juker.Host.Diagnostics;

namespace Juker.Host;

/// <summary>Compõe os módulos do <see cref="ModuleCatalog"/> no host: único ponto de composição (ADR-0001).</summary>
internal static class ModuleCatalogExtensions
{
    public static IServiceCollection AddJukerModules(this IServiceCollection services, IConfiguration configuration)
    {
        List<string> composed = [];
        foreach (var module in ModuleCatalog.All)
        {
            module.Add(services, configuration);
            composed.Add(module.Name);
        }

        // Fonte de /_diag/modules: o que foi de fato composto aqui, não o catálogo estático (SPEC-0026).
        services.AddSingleton(new ComposedModules(composed.ToArray()));

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
