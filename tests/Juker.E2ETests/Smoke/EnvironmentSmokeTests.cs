using AwesomeAssertions;
using Juker.TestKit.E2E;
using Microsoft.Playwright;
using Xunit;

namespace Juker.E2ETests.Smoke;

/// <summary>
/// Smoke do ambiente (SPEC-0025:CT-01, ADR-0013 regras 1 e 12): com <c>JUKER_E2E_BASE_URL</c>, roda contra o ambiente
/// implantado, sem AppHost e sem a API <c>/_test/*</c>; sem a variável, contra o AppHost no ambiente E2E.
/// </summary>
[Trait("Category", "Smoke")]
public sealed class EnvironmentSmokeTests(AspireAppFixture app) : IClassFixture<AspireAppFixture>
{
    [Fact]
    [Trait("Spec", "SPEC-0025:CT-01")]
    public async Task Ambiente_responde_pronto_em_health_ready()
    {
        // Dado o endereço do ambiente (AppHost, ou JUKER_E2E_BASE_URL no smoke)
        using var playwright = await Playwright.CreateAsync();
        await using var api = await playwright.APIRequest.NewContextAsync(new() { BaseURL = app.HostUrl.ToString() });

        // Quando /health/ready é consultado
        var response = await api.GetAsync("/health/ready", new() { FailOnStatusCode = false });
        await using (response)
        {
            // Então o ambiente está pronto
            response.Status.Should().Be(200);
        }
    }
}
