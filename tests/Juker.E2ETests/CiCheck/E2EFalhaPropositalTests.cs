using AwesomeAssertions;
using Juker.E2ETests.Pages.Testing;
using Juker.TestKit.E2E;
using Microsoft.Playwright;
using Xunit;

namespace Juker.E2ETests.CiCheck;

/// <summary>
/// E2E vermelho proposital da branch descartável <c>ci-check/e2e-fail</c> (SPEC-0005:IT-05): abre a página de teste
/// no Chromium e espera um texto que não existe, para que o job <c>e2e</c> falhe e publique o <c>trace.zip</c> no
/// artefato <c>playwright-traces</c>. Nunca entra na <c>main</c>.
/// </summary>
[Trait("Category", "E2E")]
public sealed class E2EFalhaPropositalTests(AspireAppFixture app, PlaywrightFixture browsers)
    : IClassFixture<AspireAppFixture>, IClassFixture<PlaywrightFixture>
{
    private const int Ok = 200;

    [Fact]
    [Trait("Spec", "SPEC-0005:IT-05")]
    public async Task Falha_de_proposito_para_verificar_os_traces_do_e2e()
    {
        // Dado a página de teste aberta no Chromium (a página grava trace e vídeo)
        var page = await browsers.NewGuestPageAsync(BrowserKind.Chromium);
        (await new PingPage(page).OpenAsync(app.HostUrl)).Should().Be(Ok);

        // Quando se espera um texto que não existe, então o teste falha (proposital)
        await Assertions.Expect(page.GetByText("texto inexistente: falha proposital da verificação SPEC-0005:IT-05"))
            .ToBeVisibleAsync(new() { Timeout = 2_000 });
    }
}
