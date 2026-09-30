using AwesomeAssertions;
using Xunit;

namespace Juker.IntegrationTests.Tooling;

[Trait("Category", "Integration")]
public sealed class QualityViolationsBuildTests
{
    private const string FixtureRelativePath = "tests/fixtures/QualityViolations";
    private static readonly string[] ViolationMarkers = ["DateTime.Now", "string.Equals(a, b)"];

    [Fact(Timeout = 600_000)]
    [Trait("Spec", "SPEC-0002:IT-03")]
    public async Task Build_falha_com_api_proibida_e_comparacao_sem_StringComparison_e_passa_sem_as_violacoes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Quando o fixture com DateTime.Now e string.Equals(a, b) é compilado
        var violations = await DotnetCli.RunAsync(["build", FixtureRelativePath], cancellationToken);

        // Então o build falha com RS0030 (API proibida) e MA0001 (sem StringComparison)
        violations.ExitCode.Should().NotBe(0, "o fixture viola o BannedSymbols.txt e a MA0001. Saída:\n{0}", violations.Output);
        violations.Output.Should().Contain("RS0030");
        violations.Output.Should().Contain("MA0001");

        // E uma cópia sem essas duas linhas, com os props da raiz, compila com sucesso
        var workspace = Directory.CreateTempSubdirectory("juker-it03-");
        try
        {
            var copy = CopyFixtureWithoutViolations(Path.Combine(workspace.FullName, "QualityViolations"));
            var clean = await DotnetCli.RunAsync(
                [
                    "build",
                    Path.Combine(copy, "QualityViolations.csproj"),
                    $"-p:DirectoryBuildPropsPath={RepositoryPaths.Combine("Directory.Build.props")}",
                    $"-p:DirectoryPackagesPropsPath={RepositoryPaths.Combine("Directory.Packages.props")}",
                    "--artifacts-path",
                    Path.Combine(workspace.FullName, "artifacts"),
                ],
                cancellationToken);

            clean.ExitCode.Should().Be(0, "sem as violações o fixture deve compilar. Saída:\n{0}", clean.Output);
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    private static string CopyFixtureWithoutViolations(string destination)
    {
        var source = RepositoryPaths.Combine("tests", "fixtures", "QualityViolations");
        Directory.CreateDirectory(destination);

        var removed = 0;
        foreach (var file in RepositoryPaths.SourceFiles(Path.Combine("tests", "fixtures", "QualityViolations"), "*"))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(file, target);
                continue;
            }

            var lines = File.ReadAllLines(file);
            var kept = lines
                .Where(line => !ViolationMarkers.Any(marker => line.Contains(marker, StringComparison.Ordinal)))
                .ToArray();
            removed += lines.Length - kept.Length;
            File.WriteAllLines(target, kept);
        }

        removed.Should().Be(2, "o fixture deve ter exatamente uma linha com DateTime.Now e outra com string.Equals(a, b)");
        return destination;
    }
}
