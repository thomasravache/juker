namespace Juker.ArchitectureTests.DesignTokens;

#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

/// <summary>
/// Política do design system (ADR-0014, regra 7): nenhuma cor literal (<c>#rgb</c>, <c>#rrggbb</c>, <c>rgb(</c>,
/// <c>hsl(</c>, valores arbitrários como <c>bg-[#…]</c>) nem <c>font-family</c> em <c>.razor</c>, <c>.razor.css</c>
/// e <c>.css</c>, exceto no arquivo de tokens (<c>src/Web/Juker.Web/Styles/theme.css</c> na solução).
/// </summary>
public static class DesignTokenPolicy
{
    /// <summary>Varre os arquivos de UI sob <paramref name="directory"/>, ignorando <paramref name="tokensFile"/>.</summary>
    public static IReadOnlyList<DesignTokenViolation> Scan(string directory, string tokensFile) =>
        throw new NotImplementedException();
}
