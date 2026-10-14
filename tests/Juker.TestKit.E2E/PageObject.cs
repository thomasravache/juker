using Microsoft.Playwright;

namespace Juker.TestKit.E2E;

/// <summary>
/// Base dos page objects (ADR-0013, regra 8): um por tela, em <c>tests/Juker.E2ETests/Pages/&lt;Área&gt;/</c>.
/// O teste descreve a jornada; o page object, os cliques e os seletores por papel e rótulo.
/// </summary>
public abstract class PageObject(IPage page)
{
    /// <summary>A página do Playwright que a tela usa.</summary>
    protected IPage Page { get; } = page;
}
