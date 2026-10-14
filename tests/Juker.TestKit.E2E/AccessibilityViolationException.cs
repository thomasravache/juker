namespace Juker.TestKit.E2E;

/// <summary>Falha do <see cref="PlaywrightFixture.AssertNoA11yViolationsAsync"/>: a página tem violações WCAG A/AA.</summary>
internal sealed class AccessibilityViolationException(string message) : Exception(message);
