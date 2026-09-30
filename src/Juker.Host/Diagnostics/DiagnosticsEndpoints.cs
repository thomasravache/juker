namespace Juker.Host.Diagnostics;

/// <summary>Rotas de diagnóstico, expostas só em Development e Testing (404 nos demais ambientes).</summary>
internal static class DiagnosticsEndpoints
{
    public const string TestingEnvironment = "Testing";

    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment(TestingEnvironment))
        {
            return endpoints;
        }

        var names = ModuleCatalog.All.Select(module => module.Name).ToArray();
        endpoints.MapGet("/_diag/modules", () => TypedResults.Ok(names))
            .ExcludeFromDescription();

        return endpoints;
    }
}
