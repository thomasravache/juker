using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Padrões comuns do Aspire para os serviços do Juker: OpenTelemetry (ADR-0010) e health checks.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>Tag dos health checks que só verificam o processo (usados por <c>/health/live</c>).</summary>
    public const string LiveTag = "live";

    /// <summary>Liveness: o processo está de pé. Não verifica dependências.</summary>
    public const string LivePath = "/health/live";

    /// <summary>Readiness: todos os health checks, inclusive o Postgres (503 quando algum falha).</summary>
    public const string ReadyPath = "/health/ready";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !IsHealthRequest(context.Request.Path))
                .AddHttpClientInstrumentation());

        // O endpoint OTLP vem do ambiente (painel do Aspire no dev; backend gerenciado nos ambientes, ADR-0010).
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

        return builder;
    }

    /// <summary>Mapeia <c>/health/live</c> e <c>/health/ready</c> em todos os ambientes (sondas do orquestrador).</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks(LivePath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(LiveTag),
        });

        app.MapHealthChecks(ReadyPath);

        return app;
    }

    private static bool IsHealthRequest(string? path) =>
        path is not null
        && (path.StartsWith(LivePath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(ReadyPath, StringComparison.OrdinalIgnoreCase));
}
