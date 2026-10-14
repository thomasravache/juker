#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0025): corpos lançam NotImplementedException até a implementação
namespace Juker.TestKit.E2E;

/// <summary>
/// Cliente da API de controle de teste do host (<c>/_test/*</c>, só no ambiente E2E): monta pré-condições.
/// A jornada em teste é sempre feita pela interface (ADR-0013, regra 4).
/// </summary>
public sealed class TestControlClient
{
    internal TestControlClient()
    {
    }

    /// <summary>Avança o relógio do servidor (<c>POST /_test/clock/advance</c>).</summary>
    public Task AdvanceClockAsync(TimeSpan by) => throw new NotImplementedException();

    /// <summary>Cria um bar para o teste (<c>POST /_test/venues</c>; o endpoint do host é da SPEC-0007).</summary>
    public Task<TestVenue> CreateVenueAsync(VenueSetup? setup = null) => throw new NotImplementedException();
}
