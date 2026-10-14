using Juker.TestKit.E2E;
using Microsoft.Playwright;
using Xunit;

namespace E2EHarnessSelfTest;

/// <summary>
/// Amostras do SPEC-0025:IT-03: um teste que falha de propósito (deve deixar trace, vídeo e captura de tela) e um que
/// passa (não deve deixar nada). Só rodam pelo IT-03.
/// </summary>
[Trait("Category", "E2E")]
public sealed class FailureDiagnosticsSampleTests(PlaywrightFixture browsers) : IClassFixture<PlaywrightFixture>
{
    private const string Content =
        """
        <!doctype html>
        <html lang="pt-BR">
          <head><title>Autoteste do harness</title></head>
          <body><main><h1>Juker</h1></main></body>
        </html>
        """;

    private static readonly LocatorAssertionsToHaveTextOptions ShortWait = new() { Timeout = 2_000 };

    [Fact]
    [Trait("Spec", "SPEC-0025:IT-03")]
    public async Task Exemplo_que_falha_de_proposito()
    {
        var page = await browsers.NewGuestPageAsync(BrowserKind.Chromium);
        await page.SetContentAsync(Content);

        // Falha de propósito: o título da página é "Juker"
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Outro título", ShortWait);
    }

    [Fact]
    [Trait("Spec", "SPEC-0025:IT-03")]
    public async Task Exemplo_que_passa()
    {
        var page = await browsers.NewGuestPageAsync(BrowserKind.Chromium);
        await page.SetContentAsync(Content);

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Juker", ShortWait);
    }
}
