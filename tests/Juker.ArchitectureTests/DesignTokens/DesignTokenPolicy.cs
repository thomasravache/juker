using System.Text.RegularExpressions;

namespace Juker.ArchitectureTests.DesignTokens;

/// <summary>
/// Política do design system (ADR-0014, regra 7): nenhuma cor literal (<c>#rgb</c>, <c>#rrggbb</c>, <c>rgb(</c>,
/// <c>hsl(</c>, valores arbitrários como <c>bg-[#…]</c>) nem <c>font-family</c> em <c>.razor</c>, <c>.razor.css</c>
/// e <c>.css</c>, exceto no arquivo de tokens (<c>src/Web/Juker.Web/Styles/theme.css</c> na solução).
/// </summary>
public static partial class DesignTokenPolicy
{
    // .razor.css também termina em .css
    private static readonly string[] UiFilePatterns = ["*.razor", "*.css"];

    private static readonly string[] BuildOutputDirectories = ["bin", "obj"];

    /// <summary>Varre os arquivos de UI sob <paramref name="directory"/>, ignorando <paramref name="tokensFile"/>.</summary>
    public static IReadOnlyList<DesignTokenViolation> Scan(string directory, string tokensFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokensFile);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var tokens = Path.GetFullPath(tokensFile);
        return UiFilePatterns
            .SelectMany(pattern => Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .Where(file => !string.Equals(file, tokens, StringComparison.Ordinal) && !IsBuildOutput(directory, file))
            .Order(StringComparer.Ordinal)
            .SelectMany(ScanFile)
            .ToList();
    }

    private static IEnumerable<DesignTokenViolation> ScanFile(string file)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(file))
        {
            lineNumber++;
            foreach (Match match in LiteralToken().Matches(line))
            {
                yield return new DesignTokenViolation(file, lineNumber, match.Value);
            }
        }
    }

    private static bool IsBuildOutput(string root, string file) =>
        Path.GetRelativePath(root, file)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => BuildOutputDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Em ordem de preferência (a primeira alternativa que casa vence, então <c>bg-[#fff]</c> sai inteiro):
    /// valor arbitrário do Tailwind com cor; cor hexadecimal (depois de espaço, <c>:</c>, <c>[</c>, <c>(</c>, <c>,</c>
    /// ou <c>=</c>, para não confundir com âncoras como <c>href="#topo"</c>); funções de cor; <c>font-family</c>.
    /// </summary>
    [GeneratedRegex(
        @"[\w-]+-\[(?:#[0-9a-f]{3,8}|(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch)\([^\]]*\))\]" +
        @"|(?<=[\s:\[(,=]|^)#(?:[0-9a-f]{8}|[0-9a-f]{6}|[0-9a-f]{3,4})\b" +
        @"|\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch)\(" +
        @"|font-family",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1_000)]
    private static partial Regex LiteralToken();
}
