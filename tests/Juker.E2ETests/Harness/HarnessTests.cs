using System.Diagnostics;
using AwesomeAssertions;
using Juker.E2ETests.Pages.Testing;
using Juker.TestKit.E2E;
using Microsoft.Playwright;
using Xunit;

namespace Juker.E2ETests.Harness;

[Trait("Category", "E2E")]
public sealed class HarnessTests
{
    private const int TestTimeoutMilliseconds = 600_000;
    private const int Ok = 200;
    private const string ReadyPath = "/health/ready";

    // SPEC-0025, seção 5: subida do AppHost no E2E em menos de 60 s com as imagens em cache.
    private static readonly TimeSpan StartupBudget = TimeSpan.FromSeconds(60);

    private static readonly BrowserKind[] GuestBrowsers = [BrowserKind.Chromium, BrowserKind.WebKit];

    [Fact(Timeout = TestTimeoutMilliseconds)]
    [Trait("Spec", "SPEC-0025:IT-01")]
    public async Task AppHost_e_navegadores_do_harness_ficam_prontos_no_ambiente_E2E()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado o AppHost iniciado pela AspireAppFixture (ambiente E2E, Postgres em container do próprio AppHost)
        var started = Stopwatch.GetTimestamp();
        await using var app = new AspireAppFixture();
        await app.InitializeAsync();

        // Quando o host fica de pé, então /health/ready responde 200 em menos de 60 s
        using var playwright = await Playwright.CreateAsync();
        await using var api = await playwright.APIRequest.NewContextAsync(new() { BaseURL = app.HostUrl.ToString() });
        var ready = await HttpPolling.WaitForStatusAsync(api, ReadyPath, Ok, StartupBudget, cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(started);
        ready.Should().Be(Ok, "o host sobe pronto no ambiente E2E");
        elapsed.Should().BeLessThan(StartupBudget, "a subida do AppHost no E2E fica abaixo de 60 s com as imagens em cache");

        // E a API de controle do fixture alcança o host no ambiente E2E
        await app.Control.AdvanceClockAsync(TimeSpan.FromMinutes(15));

        // E a página de teste abre no Chromium e no WebKit, com o perfil do cliente e sem violações axe (A/AA)
        await using var browsers = new PlaywrightFixture();
        await browsers.InitializeAsync();
        foreach (var kind in GuestBrowsers)
        {
            var page = await browsers.NewGuestPageAsync(kind);
            var ping = new PingPage(page);
            (await ping.OpenAsync(app.HostUrl)).Should().Be(Ok, "/_test/ping existe no ambiente E2E ({0})", kind);
            ping.BrowserName.Should().BeEquivalentTo(kind.ToString());
            ping.Viewport.Should().BeEquivalentTo(new { Width = 390, Height = 844 });
            (await ping.LanguageAsync()).Should().Be("pt-BR");
            (await ping.TimeZoneAsync()).Should().Be("America/Sao_Paulo");
            await browsers.AssertNoA11yViolationsAsync(page);
        }

        // E as páginas da TV e do painel abrem no Chromium com os viewports do contrato
        var tv = new PingPage(await browsers.NewTvPageAsync());
        (await tv.OpenAsync(app.HostUrl)).Should().Be(Ok);
        tv.BrowserName.Should().Be("chromium");
        tv.Viewport.Should().BeEquivalentTo(new { Width = 1920, Height = 1080 });

        var admin = new PingPage(await browsers.NewAdminPageAsync(mobile: false));
        (await admin.OpenAsync(app.HostUrl)).Should().Be(Ok);
        admin.BrowserName.Should().Be("chromium");
        admin.Viewport.Should().BeEquivalentTo(new { Width = 1280, Height = 800 });

        var adminMobile = new PingPage(await browsers.NewAdminPageAsync(mobile: true));
        (await adminMobile.OpenAsync(app.HostUrl)).Should().Be(Ok);
        adminMobile.BrowserName.Should().Be("chromium");
        adminMobile.Viewport.Should().BeEquivalentTo(new { Width = 390, Height = 844 });
    }

    [Fact]
    [Trait("Spec", "SPEC-0025:IT-01")]
    public async Task Verificacao_axe_falha_numa_pagina_com_violacao_de_acessibilidade()
    {
        // Dado uma página com uma imagem sem texto alternativo (regra image-alt, WCAG A)
        await using var browsers = new PlaywrightFixture();
        await browsers.InitializeAsync();
        var page = await browsers.NewGuestPageAsync(BrowserKind.Chromium);
        await page.SetContentAsync(
            """
            <!doctype html>
            <html lang="pt-BR">
              <head><title>Violação de acessibilidade</title></head>
              <body><main><h1>Juker</h1><img src="data:image/gif;base64,R0lGODlhAQABAAAAACw="></main></body>
            </html>
            """);

        // Quando a página passa pela verificação axe-core
        var check = () => browsers.AssertNoA11yViolationsAsync(page);

        // Então a verificação falha citando a regra violada
        var failure = await check.Should().ThrowAsync<Exception>();
        failure.Which.Should().NotBeOfType<NotImplementedException>();
        failure.Which.Message.Should().Contain("image-alt");
    }
}
