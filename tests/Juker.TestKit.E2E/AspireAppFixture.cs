#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0025): corpos lançam NotImplementedException até a implementação
using Xunit;

namespace Juker.TestKit.E2E;

/// <summary>
/// Sobe o <c>Juker.AppHost</c> (host e Postgres em container) com <c>ASPNETCORE_ENVIRONMENT=E2E</c>, uma vez por
/// execução (SPEC-0025, ADR-0013 regra 1). Com <c>JUKER_E2E_BASE_URL</c> definido, não sobe o AppHost: os testes
/// <c>Smoke</c> rodam contra a URL informada, sem a API <c>/_test/*</c>.
/// </summary>
public sealed class AspireAppFixture : IAsyncLifetime
{
    /// <summary>Endereço base do host (o do AppHost, ou <c>JUKER_E2E_BASE_URL</c> no smoke).</summary>
    public Uri HostUrl => throw new NotImplementedException();

    /// <summary>Cliente da API de controle de teste do host (<c>/_test/*</c>, só no ambiente E2E).</summary>
    public TestControlClient Control => throw new NotImplementedException();

    public ValueTask InitializeAsync() => throw new NotImplementedException();

    // Esqueleto do contrato (SPEC-0025): sem recursos para liberar até a implementação.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
