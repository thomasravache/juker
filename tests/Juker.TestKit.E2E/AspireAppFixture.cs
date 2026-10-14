using Xunit;

namespace Juker.TestKit.E2E;

/// <summary>
/// Sobe o <c>Juker.AppHost</c> (host e Postgres em container) com <c>ASPNETCORE_ENVIRONMENT=E2E</c>, uma vez por
/// execução (SPEC-0025, ADR-0013 regra 1). Com <c>JUKER_E2E_BASE_URL</c> definido, não sobe o AppHost: os testes
/// <c>Smoke</c> rodam contra a URL informada, sem a API <c>/_test/*</c>.
/// </summary>
/// <remarks>
/// Todas as instâncias do processo compartilham o mesmo AppHost: a primeira o sobe e a última a ser liberada o
/// derruba. Cada teste cria os próprios dados (bar com slug único), então o compartilhamento não acopla testes.
/// </remarks>
public sealed class AspireAppFixture : IAsyncLifetime
{
    /// <summary>Variável com o endereço de um ambiente implantado (smoke de staging e produção).</summary>
    internal const string BaseUrlVariable = "JUKER_E2E_BASE_URL";

    private Uri? _hostUrl;
    private TestControlClient? _control;
    private HttpClient? _http;
    private bool _holdsSharedApp;

    /// <summary>Endereço base do host (o do AppHost, ou <c>JUKER_E2E_BASE_URL</c> no smoke).</summary>
    public Uri HostUrl => _hostUrl ?? throw NotInitialized();

    /// <summary>Cliente da API de controle de teste do host (<c>/_test/*</c>, só no ambiente E2E).</summary>
    public TestControlClient Control
    {
        get
        {
            if (_hostUrl is null)
            {
                throw NotInitialized();
            }

            return _control ?? throw new InvalidOperationException(
                $"A API de teste /_test/* não existe com {BaseUrlVariable} definido (ambiente implantado): só os testes Smoke rodam contra a URL.");
        }
    }

    public async ValueTask InitializeAsync()
    {
        var baseUrl = Environment.GetEnvironmentVariable(BaseUrlVariable);
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _hostUrl = new Uri(baseUrl, UriKind.Absolute);
            return;
        }

        var app = await SharedAppHost.AcquireAsync(TestContext.Current.CancellationToken);
        _holdsSharedApp = true;
        _hostUrl = app.HostUrl;
        _http = app.CreateHostClient();
        _control = new TestControlClient(_http);
    }

    public async ValueTask DisposeAsync()
    {
        _http?.Dispose();
        _http = null;
        _control = null;
        if (_holdsSharedApp)
        {
            _holdsSharedApp = false;
            await SharedAppHost.ReleaseAsync();
        }
    }

    private static InvalidOperationException NotInitialized() =>
        new($"{nameof(AspireAppFixture)} ainda não foi inicializado (InitializeAsync).");
}
