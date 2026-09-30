using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using AwesomeAssertions;
using Xunit;

namespace Juker.IntegrationTests.Tooling;

[Trait("Category", "Integration")]
public sealed class AppHostTests
{
    private const string PostgresResource = "postgres";
    private const string HostResource = "juker-host";
    private const int TestTimeoutMilliseconds = 600_000;
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromMinutes(3);
    private static readonly string[] ExpectedModules = ["Venues", "Sessions", "Catalog", "Queue", "Playback"];

    [Fact(Timeout = TestTimeoutMilliseconds)]
    [Trait("Spec", "SPEC-0002:IT-01")]
    public async Task AppHost_sobe_host_pronto_e_diagnostico_lista_os_cinco_modulos()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado o AppHost iniciado com Postgres real em container
        await using var app = await StartAppHostAsync(cancellationToken);
        using var client = app.CreateHttpClient(HostResource);

        // Quando o host fica de pé, então /health/ready responde 200
        var ready = await HealthPolling.WaitForStatusAsync(client, "/health/ready", HttpStatusCode.OK, HealthTimeout, cancellationToken);
        ready.Should().Be(HttpStatusCode.OK);

        // E /_diag/modules (Development) lista os cinco módulos, em ordem
        using var response = await client.GetAsync(new Uri("/_diag/modules", UriKind.Relative), cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var modules = await response.Content.ReadFromJsonAsync<string[]>(cancellationToken);
        modules.Should().Equal(ExpectedModules);
    }

    [Fact(Timeout = TestTimeoutMilliseconds)]
    [Trait("Spec", "SPEC-0002:IT-02")]
    public async Task Postgres_parado_deixa_ready_503_e_live_200()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado o AppHost em execução com o host pronto
        await using var app = await StartAppHostAsync(cancellationToken);
        using var client = app.CreateHttpClient(HostResource);
        var initial = await HealthPolling.WaitForStatusAsync(client, "/health/ready", HttpStatusCode.OK, HealthTimeout, cancellationToken);
        initial.Should().Be(HttpStatusCode.OK, "o host precisa estar pronto antes de o Postgres parar");

        // Quando o recurso postgres é parado
        var stop = await app.ResourceCommands.ExecuteCommandAsync(PostgresResource, KnownResourceCommands.StopCommand, cancellationToken);
        stop.Success.Should().BeTrue(stop.Message);

        // Então /health/ready responde 503 e /health/live continua 200
        var ready = await HealthPolling.WaitForStatusAsync(client, "/health/ready", HttpStatusCode.ServiceUnavailable, HealthTimeout, cancellationToken);
        ready.Should().Be(HttpStatusCode.ServiceUnavailable);

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), cancellationToken);
        live.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<DistributedApplication> StartAppHostAsync(CancellationToken cancellationToken)
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Juker_AppHost>(
            ["--environment", "Development"],
            cancellationToken);

        builder.Resources.Select(resource => resource.Name)
            .Should().Contain([PostgresResource, HostResource], "o AppHost declara o Postgres e o host");

        var app = await builder.BuildAsync(cancellationToken);
        try
        {
            await app.StartAsync(cancellationToken);
            await app.ResourceNotifications.WaitForResourceAsync(HostResource, KnownResourceStates.Running, cancellationToken);
            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }
}
