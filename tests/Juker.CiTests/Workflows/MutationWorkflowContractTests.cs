using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Juker.CiTests.Tooling;
using Xunit;

namespace Juker.CiTests.Workflows;

/// <summary>
/// SPEC-0005:CT-01 — o contrato do <c>mutation.yml</c> (seção 6, contrato v3): semanal no domingo às 03:17 UTC e sob
/// demanda, fora dos checks de PR; Stryker.NET como ferramenta local, só nas classes listadas nos
/// <c>stryker-config.json</c>, com <c>break</c> em 80 e o relatório publicado como <c>mutation-report</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class MutationWorkflowContractTests
{
    private const int BreakThreshold = 80;

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Mutation_roda_toda_semana_no_domingo_0317_UTC_e_sob_demanda_sem_gatilho_de_PR()
    {
        // Dado o mutation.yml
        var triggers = LoadMutation().Triggers;

        // Então ele roda sob demanda e num único cron semanal: domingo às 03:17 UTC (emenda v3)
        triggers.Should().ContainKey("workflow_dispatch");
        triggers.Should().ContainKey("schedule");
        var crons = triggers["schedule"].Items().Select(item => item.Text("cron")).ToList();
        crons.Should().ContainSingle().Which.Should().MatchRegex(SundayAt0317Pattern);

        // E não é check de PR nem de push (não bloqueia PRs)
        triggers.Keys.Should().NotContain(["pull_request", "pull_request_target", "push"]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Mutation_roda_o_stryker_com_um_stryker_config_e_publica_o_mutation_report()
    {
        // Dado o mutation.yml
        var jobs = LoadMutation().Jobs;

        // Então algum job roda o dotnet stryker com -f/--config-file sobre um stryker-config.json
        var stryker = jobs.Values.Where(job => StrykerRun().IsMatch(Workflow.RunText(job))).ToList();
        stryker.Should().NotBeEmpty("o mutation.yml roda 'dotnet stryker -f <...>/stryker-config.json'");
        stryker.Should().Contain(job => job.AllScalars().Any(value => value.Contains("stryker-config.json", StringComparison.Ordinal)));

        // E publica o relatório como mutation-report
        stryker.SelectMany(job => Workflow.StepsUsing(job, "actions/upload-artifact"))
            .Select(step => step.Child("with").Text("name"))
            .Should().Contain("mutation-report");
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Stryker_e_ferramenta_local_do_repositorio()
    {
        // Dado o .config/dotnet-tools.json
        using var manifest = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.Combine(".config", "dotnet-tools.json")));

        // Então o dotnet-stryker é uma ferramenta local fixada por versão
        manifest.RootElement.GetProperty("tools").TryGetProperty("dotnet-stryker", out var tool).Should()
            .BeTrue("o Stryker.NET é ferramenta local em .config/dotnet-tools.json (ADR-0014)");
        tool.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Cada_stryker_config_lista_as_classes_criticas_e_quebra_em_80()
    {
        // Dado os stryker-config.json em tests/
        var configs = Directory.EnumerateFiles(RepositoryPaths.Combine("tests"), "stryker-config.json", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .ToList();
        configs.Should().NotBeEmpty("as classes críticas ficam listadas em tests/**/stryker-config.json");

        // Então cada um lista arquivos em mutate e quebra abaixo de 80
        foreach (var path in configs)
        {
            using var config = JsonDocument.Parse(File.ReadAllText(path));
            var stryker = config.RootElement.GetProperty("stryker-config");
            var relative = Path.GetRelativePath(RepositoryPaths.Root, path);

            stryker.TryGetProperty("mutate", out var mutate).Should().BeTrue("{0} lista as classes críticas em 'mutate'", relative);
            mutate.ValueKind.Should().Be(JsonValueKind.Array);
            mutate.GetArrayLength().Should().BePositive("{0} lista ao menos uma classe crítica", relative);

            stryker.TryGetProperty("thresholds", out var thresholds).Should().BeTrue("{0} declara thresholds", relative);
            thresholds.TryGetProperty("break", out var breakAt).Should().BeTrue("{0} declara thresholds.break", relative);
            breakAt.GetInt32().Should().Be(BreakThreshold, "{0} quebra em 80% (ADR-0014)", relative);
        }
    }

    private static Workflow LoadMutation()
    {
        Workflow.Exists(Workflow.MutationFile).Should().BeTrue("o workflow .github/workflows/mutation.yml existe (SPEC-0005, seção 6)");
        return Workflow.Load(Workflow.MutationFile);
    }

    private static bool IsBuildOutput(string path) =>
        Path.GetRelativePath(RepositoryPaths.Root, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj" or "StrykerOutput");

    // minuto 17, hora 3, qualquer dia do mês e mês, domingo (0, 7 ou SUN)
    private const string SundayAt0317Pattern = @"^17\s+3\s+\*\s+\*\s+(0|7|SUN|sun|Sun)$";

    [GeneratedRegex(@"dotnet( tool run)? (dotnet-)?stryker\b.*(-f|--config-file) ", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex StrykerRun();
}
