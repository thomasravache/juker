#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

namespace Juker.TestKit;

/// <summary>Servidor HTTP fake (WireMock.Net) que responde com as gravações de <c>Recordings/*.json</c> (ADR-0012, regra 8).</summary>
public sealed class FakeHttpServer : IDisposable
{
    public FakeHttpServer() => throw new NotImplementedException();

    /// <summary>URL base do servidor fake, para apontar os clientes HTTP tipados.</summary>
    public Uri BaseUrl => throw new NotImplementedException();

    public void Dispose() => throw new NotImplementedException();
}
