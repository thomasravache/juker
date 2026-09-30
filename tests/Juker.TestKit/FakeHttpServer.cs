using WireMock.Server;

namespace Juker.TestKit;

/// <summary>Servidor HTTP fake (WireMock.Net) que responde com as gravações de <c>Recordings/*.json</c> (ADR-0012, regra 8).</summary>
/// <remarks>
/// As gravações são mapeamentos estáticos do WireMock, regravados por <c>tools/recordings/refresh-youtube.sh</c>.
/// Requisições sem gravação correspondente recebem 404 do WireMock (nunca chegam à API real).
/// </remarks>
public sealed class FakeHttpServer : IDisposable
{
    private const string RecordingsDirectory = "Recordings";

    private readonly WireMockServer _server;

    public FakeHttpServer()
    {
        _server = WireMockServer.Start();

        var recordings = Path.Combine(AppContext.BaseDirectory, RecordingsDirectory);
        if (Directory.Exists(recordings))
        {
            _server.ReadStaticMappings(recordings);
        }
    }

    /// <summary>URL base do servidor fake, para apontar os clientes HTTP tipados.</summary>
    public Uri BaseUrl => new(_server.Url!, UriKind.Absolute);

    public void Dispose() => _server.Dispose();
}
