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

        // Resolvido do DI a cada requisição: reflete a composição do host (SPEC-0026).
        endpoints.MapGet("/_diag/modules", (ComposedModules modules) => TypedResults.Ok(modules.Names))
            .ExcludeFromDescription();

        return endpoints;
    }
}
