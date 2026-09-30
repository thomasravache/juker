using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Juker.IntegrationTests.Realtime;

/// <summary>
/// Controle negativo do ARQ-08 (ADR-0012, regra 9): o cliente HTTP/SignalR do IT fala com o TestServer real pelo
/// handler que o próprio TestServer cria (<c>HttpMessageHandlerFactory = _ =&gt; server.CreateHandler()</c>).
/// Não é mock de HttpMessageHandler e não pode ser apontado.
/// </summary>
public static class TestServerClientSetup
{
    public static IHttpClientBuilder UseTestServer(IServiceCollection services, TestServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        return services
            .AddHttpClient("juker-api")
            .ConfigurePrimaryHttpMessageHandler(() => server.CreateHandler());
    }
}
