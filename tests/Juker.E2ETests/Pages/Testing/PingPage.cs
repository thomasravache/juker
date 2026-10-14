using Juker.TestKit.E2E;
using Microsoft.Playwright;

namespace Juker.E2ETests.Pages.Testing;

/// <summary>Página de teste <c>/_test/ping</c> (HTML mínimo, só no ambiente E2E; SPEC-0025:IT-01).</summary>
internal sealed class PingPage(IPage page) : PageObject(page)
{
    private const string Path = "/_test/ping";

    public PageViewportSizeResult? Viewport => Page.ViewportSize;

    public string? BrowserName => Page.Context.Browser?.BrowserType.Name;

    /// <summary>Abre a página e devolve o status HTTP da navegação.</summary>
    public async Task<int?> OpenAsync(Uri hostUrl)
    {
        ArgumentNullException.ThrowIfNull(hostUrl);

        var response = await Page.GotoAsync(new Uri(hostUrl, Path).ToString());
        return response?.Status;
    }

    public Task<string> LanguageAsync() => Page.EvaluateAsync<string>("() => navigator.language");

    public Task<string> TimeZoneAsync() => Page.EvaluateAsync<string>("() => Intl.DateTimeFormat().resolvedOptions().timeZone");
}
