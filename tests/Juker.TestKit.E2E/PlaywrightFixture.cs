using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Xunit;

namespace Juker.TestKit.E2E;

/// <summary>
/// Navegadores do Playwright para os E2E (SPEC-0025, ADR-0013 regras 2, 10 e 11). Quando o teste falha, grava
/// <c>trace.zip</c>, <c>video.webm</c> e <c>screenshot.png</c> em <c>TestResults/playwright/&lt;teste&gt;/</c>.
/// </summary>
/// <remarks>
/// Cada página nasce num contexto próprio, com trace e vídeo ligados. O resultado do teste chega pelo gancho
/// <see cref="PlaywrightDiagnosticsAttribute"/> (aplicado no assembly de teste): na falha, o diagnóstico vai para a
/// pasta do teste, na saída do projeto; quando o teste passa, nada fica gravado. Cada navegador só é aberto no primeiro
/// uso (um teste só com Chromium não precisa do WebKit instalado).
/// </remarks>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private const string Locale = "pt-BR";
    private const string TimezoneId = "America/Sao_Paulo";

    private static readonly ViewportSize Phone = new() { Width = 390, Height = 844 };
    private static readonly ViewportSize Tv = new() { Width = 1920, Height = 1080 };
    private static readonly ViewportSize Desktop = new() { Width = 1280, Height = 800 };

    // WCAG 2.0, 2.1 e 2.2, níveis A e AA (ADR-0013, regra 11)
    private static readonly List<string> WcagTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

    private readonly Lock _lock = new();
    private readonly Dictionary<BrowserKind, Task<IBrowser>> _browsers = [];
    private readonly List<PageRecording> _recordings = [];
    private readonly List<Task> _pending = [];
    private IPlaywright? _playwright;

    /// <summary>Página do cliente: 390×844, pt-BR, fuso America/Sao_Paulo.</summary>
    public Task<IPage> NewGuestPageAsync(BrowserKind kind) => NewPageAsync(kind, Phone);

    /// <summary>Página da TV: Chromium, 1920×1080.</summary>
    public Task<IPage> NewTvPageAsync() => NewPageAsync(BrowserKind.Chromium, Tv);

    /// <summary>Página do painel: Chromium, 1280×800 ou 390×844 (<paramref name="mobile"/>).</summary>
    public Task<IPage> NewAdminPageAsync(bool mobile) => NewPageAsync(BrowserKind.Chromium, mobile ? Phone : Desktop);

    /// <summary>Falha se o axe-core encontrar violações das regras WCAG A/AA na página.</summary>
    public async Task AssertNoA11yViolationsAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        EnsureInitialized();

        var result = await page.RunAxe(new AxeRunOptions { RunOnly = new RunOnlyOptions { Type = "tag", Values = WcagTags } });
        if (result.Violations.Length == 0)
        {
            return;
        }

        var violations = result.Violations.Select(violation =>
            $"- {violation.Id} ({violation.Impact}): {violation.Help} [{string.Join(", ", violation.Tags)}]{Environment.NewLine}" +
            string.Join(Environment.NewLine, violation.Nodes.Select(node => $"    {node.Target}: {node.Html}")));
        throw new AccessibilityViolationException(
            $"axe-core encontrou {result.Violations.Length} violação(ões) WCAG A/AA em {page.Url}:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    public async ValueTask InitializeAsync() => _playwright = await Playwright.CreateAsync();

    public async ValueTask DisposeAsync()
    {
        // Finalizações iniciadas pelo gancho ao fim de cada teste
        await Task.WhenAll(TakePending());

        // Contextos ainda abertos: o teste em andamento descarta a fixture (await using) ou a página nasceu fora de um
        // teste. O diagnóstico vai para uma área temporária e o gancho decide quando o resultado sair.
        List<PageRecording> open;
        lock (_lock)
        {
            open = [.. _recordings];
            _recordings.Clear();
        }

        foreach (var recording in open)
        {
            await PlaywrightDiagnostics.FinalizeOnDisposeAsync(recording);
        }

        await Task.WhenAll(TakePending());

        List<Task<IBrowser>> browsers;
        lock (_lock)
        {
            browsers = [.. _browsers.Values];
            _browsers.Clear();
        }

        foreach (var browser in browsers)
        {
            try
            {
                await (await browser).DisposeAsync();
            }
            catch (PlaywrightException)
            {
                // o navegador não chegou a abrir: a falha já apareceu no teste que o pediu
            }
        }

        _playwright?.Dispose();
        _playwright = null;
    }

    /// <summary>Acompanha uma finalização iniciada fora da fixture (gancho do fim do teste).</summary>
    internal void Track(Task finalization)
    {
        lock (_lock)
        {
            _pending.Add(finalization);
        }
    }

    internal void Forget(PageRecording recording)
    {
        lock (_lock)
        {
            _recordings.Remove(recording);
        }
    }

    private List<Task> TakePending()
    {
        lock (_lock)
        {
            var pending = _pending.ToList();
            _pending.Clear();
            return pending;
        }
    }

    private async Task<IPage> NewPageAsync(BrowserKind kind, ViewportSize viewport)
    {
        var test = PlaywrightDiagnostics.CurrentTest();
        var browser = await BrowserAsync(kind);
        var videoDirectory = PlaywrightDiagnostics.NewStagingDirectory("video");

        var context = await browser.NewContextAsync(new()
        {
            ViewportSize = viewport,
            Locale = Locale,
            TimezoneId = TimezoneId,
            RecordVideoDir = videoDirectory,
        });

        try
        {
            await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = false });
            var page = await context.NewPageAsync();
            var recording = new PageRecording(this, context, page, videoDirectory, test);
            lock (_lock)
            {
                _recordings.Add(recording);
            }

            PlaywrightDiagnostics.Register(recording);
            return page;
        }
        catch
        {
            await context.CloseAsync();
            throw;
        }
    }

    private IPlaywright EnsureInitialized() => _playwright ?? throw new InvalidOperationException(
        $"{nameof(PlaywrightFixture)} ainda não foi inicializada (InitializeAsync).");

    private Task<IBrowser> BrowserAsync(BrowserKind kind)
    {
        var playwright = EnsureInitialized();

        lock (_lock)
        {
            if (!_browsers.TryGetValue(kind, out var browser) || browser.IsFaulted || browser.IsCanceled)
            {
                var type = kind switch
                {
                    BrowserKind.Chromium => playwright.Chromium,
                    BrowserKind.WebKit => playwright.Webkit,
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Navegador não suportado."),
                };
                browser = type.LaunchAsync();
                _browsers[kind] = browser;
            }

            return browser;
        }
    }
}
