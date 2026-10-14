using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Juker.TestKit.E2E;

/// <summary>
/// O AppHost compartilhado pelas instâncias do <see cref="AspireAppFixture"/> no processo de teste (uma subida por
/// execução enquanto houver quem o use): conta as referências e derruba o AppHost quando a última é liberada.
/// </summary>
internal sealed class SharedAppHost : IAsyncDisposable
{
    private const string HostResource = "juker-host";
    private const string HttpEndpoint = "http";
    private const string EnvironmentVariable = "ASPNETCORE_ENVIRONMENT";
    private const string E2EEnvironment = "E2E";
    private const string ReadyPath = "/health/ready";

    // Prazo da subida (primeira execução pode baixar a imagem do Postgres); a meta com imagens em cache é < 60 s.
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReadyPollInterval = TimeSpan.FromMilliseconds(250);

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static SharedAppHost? _current;
    private static int _references;

    private readonly DistributedApplication _app;

    private SharedAppHost(DistributedApplication app, Uri hostUrl)
    {
        _app = app;
        HostUrl = hostUrl;
    }

    public Uri HostUrl { get; }

    /// <summary>Um cliente HTTP do host (pela fábrica do Aspire).</summary>
    public HttpClient CreateHostClient() => _app.CreateHttpClient(HostResource, HttpEndpoint);

    public static async Task<SharedAppHost> AcquireAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            _current ??= await StartAsync(cancellationToken);
            _references++;
            return _current;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task ReleaseAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (--_references > 0 || _current is null)
            {
                return;
            }

            var current = _current;
            _current = null;
            await current.DisposeAsync();
        }
        finally
        {
            Gate.Release();
        }
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private static async Task<SharedAppHost> StartAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StartupTimeout);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Juker_AppHost>(timeout.Token);

        // O host roda no ambiente E2E (relógio controlável e /_test/*), acima do perfil de inicialização do projeto.
        builder.CreateResourceBuilder<ProjectResource>(HostResource)
            .WithEnvironment(EnvironmentVariable, E2EEnvironment);

        var app = await builder.BuildAsync(timeout.Token);
        try
        {
            await app.StartAsync(timeout.Token);
            await app.ResourceNotifications.WaitForResourceAsync(HostResource, KnownResourceStates.Running, timeout.Token);
            await WaitUntilReadyAsync(app, timeout.Token);
            return new SharedAppHost(app, app.GetEndpoint(HostResource, HttpEndpoint));
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    /// <summary>Espera por condição (ADR-0013, regra 9): <c>/health/ready</c> = 200 a cada tique, até o prazo.</summary>
    private static async Task WaitUntilReadyAsync(DistributedApplication app, CancellationToken cancellationToken)
    {
        using var client = app.CreateHttpClient(HostResource, HttpEndpoint);
        using var timer = new PeriodicTimer(ReadyPollInterval);
        HttpStatusCode? last = null;
        do
        {
            try
            {
                using var response = await client.GetAsync(new Uri(ReadyPath, UriKind.Relative), cancellationToken);
                last = response.StatusCode;
                if (last == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // o host ainda não aceita conexões: tenta de novo no próximo tique
            }
        }
        while (await WaitForNextTickAsync(timer, cancellationToken));

        throw new TimeoutException($"O host não ficou pronto em {ReadyPath} (último status: {last?.ToString() ?? "sem resposta"}).");
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
