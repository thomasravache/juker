using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

namespace Juker.TestKit;

/// <summary>
/// Host real do Juker para IT (ADR-0012): banco da classe, <see cref="FakeTimeProvider"/> no lugar do
/// <see cref="TimeProvider"/>, <c>Outbox:DispatcherEnabled=false</c> e os esquemas de auth reais.
/// </summary>
public sealed class JukerApiFactory(PostgresFixture db) : WebApplicationFactory<Program>
{
    // Scaffold (SPEC-0003): o banco da classe é criado a partir desta fixture na implementação.
    private readonly PostgresFixture _db = db;

    /// <summary>Relógio controlado pelo teste; substitui o <see cref="TimeProvider"/> do host.</summary>
    public FakeTimeProvider Time => throw new NotImplementedException();

    /// <summary>Ponto de extensão para os emissores de credenciais de teste (SPEC-0009).</summary>
    public void UseTestIssuers(Action<IServiceCollection> configure) => throw new NotImplementedException();
}
