using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace Juker.IntegrationTests.Tooling;

[Trait("Category", "Unit")]
public sealed class AnalyzerConfigurationTests
{
    private const string PragmaDisable = "#pragma warning disable";

    [Fact]
    [Trait("Spec", "SPEC-0002:UT-03")]
    public void Supressoes_sao_justificadas_e_os_analisadores_estao_referenciados()
    {
        // Dado todos os arquivos .cs sob src/ e tests/
        var sources = RepositoryPaths.SourceFiles("src", "*.cs")
            .Concat(RepositoryPaths.SourceFiles("tests", "*.cs"))
            .ToList();
        sources.Should().NotBeEmpty();

        // Então todo #pragma warning disable vem com comentário de justificativa (mesma linha ou anterior)
        var unjustified = sources.SelectMany(UnjustifiedPragmas).ToList();
        unjustified.Should().BeEmpty("toda supressão precisa de um comentário com o motivo");

        // E o Directory.Build.props referencia o Meziantou e o BannedApiAnalyzers
        var buildProps = RepositoryPaths.Combine("Directory.Build.props");
        File.Exists(buildProps).Should().BeTrue("Directory.Build.props deve existir na raiz");
        var referenced = XDocument.Load(buildProps)
            .Descendants()
            .Where(element => element.Name.LocalName.EndsWith("PackageReference", StringComparison.Ordinal))
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .ToList();

        referenced.Should().Contain("Meziantou.Analyzer");
        referenced.Should().Contain("Microsoft.CodeAnalysis.BannedApiAnalyzers");
    }

    private static IEnumerable<string> UnjustifiedPragmas(string file)
    {
        var lines = File.ReadAllLines(file);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (!line.StartsWith(PragmaDisable, StringComparison.Ordinal))
            {
                continue;
            }

            var sameLine = HasComment(line[PragmaDisable.Length..]);
            var previousLine = index > 0 && IsCommentLine(lines[index - 1]);
            if (!sameLine && !previousLine)
            {
                yield return $"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{index + 1}";
            }
        }
    }

    private static bool HasComment(string text)
    {
        var start = text.IndexOf("//", StringComparison.Ordinal);
        return start >= 0 && !string.IsNullOrWhiteSpace(text[(start + 2)..]);
    }

    private static bool IsCommentLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith("//", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(trimmed[2..]);
    }
}
