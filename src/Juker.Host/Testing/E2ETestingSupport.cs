using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juker.Host.Testing;

/// <summary>
/// Suporte a E2E no host (SPEC-0025, ADR-0013 regras 3 e 4), registrado num único ponto e só no ambiente
/// <see cref="EnvironmentName"/>: <see cref="ControllableTimeProvider"/> no lugar do <see cref="TimeProvider"/> do
/// sistema e a API de controle <c>/_test/*</c>. Em <c>Staging</c> e <c>Production</c> nada disso existe
/// (SPEC-0025:IT-02).
/// </summary>
internal static class E2ETestingSupport
{
    /// <summary>Ambiente em que o suporte a E2E é registrado.</summary>
    public const string EnvironmentName = "E2E";

    private const string PingHtml =
        """
        <!doctype html>
        <html lang="pt-BR">
          <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Juker — página de teste</title>
          </head>
          <body>
            <main>
              <h1>Juker</h1>
              <p>Ambiente E2E pronto.</p>
            </main>
          </body>
        </html>
        """;

    /// <summary>Se o host está no ambiente E2E (o único em que este suporte é registrado).</summary>
    public static bool IsE2E(this IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment.IsEnvironment(EnvironmentName);
    }

    /// <summary>Registra o relógio controlável e os serviços da API de teste.</summary>
    public static WebApplicationBuilder AddE2ETestingSupport(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Substitui qualquer TimeProvider (o AddJukerInfrastructure só registra o do sistema com TryAdd).
        builder.Services.AddSingleton(new ControllableTimeProvider(TimeProvider.System));
        builder.Services.RemoveAll<TimeProvider>();
        builder.Services.AddSingleton<TimeProvider>(services => services.GetRequiredService<ControllableTimeProvider>());

        return builder;
    }

    /// <summary>
    /// Mapeia <c>/_test/*</c>. Ponto de extensão: as specs seguintes acrescentam aqui os atalhos de pré-condição
    /// (ex.: <c>POST /_test/venues</c> da SPEC-0007), sempre sob o mesmo registro condicional do <c>Program.cs</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var test = endpoints.MapGroup("/_test").ExcludeFromDescription();

        test.MapPost("/clock/advance", AdvanceClock);
        test.MapGet("/ping", () => TypedResults.Content(PingHtml, "text/html; charset=utf-8"));

        return endpoints;
    }

    private static IResult AdvanceClock(ClockAdvanceRequest request, ControllableTimeProvider clock)
    {
        if (request.Seconds < 0 || double.IsNaN(request.Seconds) || double.IsInfinity(request.Seconds))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["seconds"] = ["O relógio só avança: informe um número de segundos maior ou igual a zero."],
            });
        }

        clock.Advance(TimeSpan.FromSeconds(request.Seconds));
        return TypedResults.NoContent();
    }

    /// <summary>Corpo de <c>POST /_test/clock/advance</c>: <c>{ "seconds": 900 }</c>.</summary>
    internal sealed record ClockAdvanceRequest(double Seconds);
}
