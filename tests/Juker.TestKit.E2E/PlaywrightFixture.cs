#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0025): corpos lançam NotImplementedException até a implementação
using Microsoft.Playwright;
using Xunit;

namespace Juker.TestKit.E2E;

/// <summary>
/// Navegadores do Playwright para os E2E (SPEC-0025, ADR-0013 regras 2, 10 e 11). Quando o teste falha, grava
/// <c>trace.zip</c>, <c>video.webm</c> e <c>screenshot.png</c> em <c>TestResults/playwright/&lt;teste&gt;/</c>.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    /// <summary>Página do cliente: 390×844, pt-BR, fuso America/Sao_Paulo.</summary>
    public Task<IPage> NewGuestPageAsync(BrowserKind kind) => throw new NotImplementedException();

    /// <summary>Página da TV: Chromium, 1920×1080.</summary>
    public Task<IPage> NewTvPageAsync() => throw new NotImplementedException();

    /// <summary>Página do painel: Chromium, 1280×800 ou 390×844 (<paramref name="mobile"/>).</summary>
    public Task<IPage> NewAdminPageAsync(bool mobile) => throw new NotImplementedException();

    /// <summary>Falha se o axe-core encontrar violações das regras WCAG A/AA na página.</summary>
    public Task AssertNoA11yViolationsAsync(IPage page) => throw new NotImplementedException();

    public ValueTask InitializeAsync() => throw new NotImplementedException();

    // Esqueleto do contrato (SPEC-0025): sem recursos para liberar até a implementação.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
