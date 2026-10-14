#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0025): corpos lançam NotImplementedException até a implementação
namespace Juker.Host.Testing;

/// <summary>
/// Suporte a E2E no host (SPEC-0025, ADR-0013 regras 3 e 4), registrado num único ponto e só no ambiente
/// <see cref="EnvironmentName"/>: <c>ControllableTimeProvider</c> no lugar do <see cref="TimeProvider"/> do sistema e
/// a API de controle <c>/_test/*</c>. Em <c>Staging</c> e <c>Production</c> nada disso existe (SPEC-0025:IT-02).
/// </summary>
internal static class E2ETestingSupport
{
    /// <summary>Ambiente em que o suporte a E2E é registrado.</summary>
    public const string EnvironmentName = "E2E";

    /// <summary>Registra o relógio controlável e os serviços da API de teste.</summary>
    public static WebApplicationBuilder AddE2ETestingSupport(this WebApplicationBuilder builder) =>
        throw new NotImplementedException();

    /// <summary>
    /// Mapeia <c>/_test/*</c>. Ponto de extensão: as specs seguintes acrescentam aqui os atalhos de pré-condição
    /// (ex.: <c>POST /_test/venues</c> da SPEC-0007).
    /// </summary>
    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder endpoints) =>
        throw new NotImplementedException();
}
