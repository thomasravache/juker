using System.Text.RegularExpressions;
using AwesomeAssertions;
using Juker.CiTests.Tooling;
using Xunit;

namespace Juker.CiTests.Workflows;

/// <summary>
/// SPEC-0005:UT-01 — os workflows passam no <c>actionlint</c> (binário fixado em <c>tools/ci/ci-tools.sh</c>) e toda
/// action externa está fixada por SHA de 40 caracteres (ADR-0017). Também cobre o NFR de segurança verificado pelo
/// UT-01: permissões mínimas (<c>contents: read</c>; <c>packages: write</c> só no job <c>image</c>) e nenhum gatilho
/// que exponha segredos a PRs de fork (<c>pull_request_target</c>).
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class WorkflowLintTests
{
    // Primeira execução baixa o actionlint e o shellcheck (alguns MB) para o cache.
    private const int ActionlintTimeoutMilliseconds = 300_000;

    private static readonly string[] ContractWorkflows = [Workflow.CiFile, Workflow.MutationFile];

    [Fact(Timeout = ActionlintTimeoutMilliseconds)]
    [Trait("Spec", "SPEC-0005:UT-01")]
    public async Task Workflows_passam_no_actionlint_sem_erros()
    {
        // Dado o ci.yml e o mutation.yml em .github/workflows
        AssertContractWorkflowsExist();
        var files = Workflow.AllFileNames().Select(name => $".github/workflows/{name}").ToList();

        // Quando o actionlint roda sobre todos os workflows
        var result = await CiTools.ActionlintAsync(files, TestContext.Current.CancellationToken);

        // Então não há nenhum erro
        result.ExitCode.Should().Be(0, "o actionlint não aponta erros nos workflows; saída:\n{0}", result.Output);
        result.StandardOutput.Should().BeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:UT-01")]
    public void Toda_action_externa_esta_fixada_por_SHA_de_40_caracteres()
    {
        // Dado os workflows do repositório
        AssertContractWorkflowsExist();

        // Quando cada uses: é lido
        var unpinned = Workflow.AllFileNames()
            .Select(Workflow.Load)
            .SelectMany(workflow => workflow.AllUses().Where(uses => !IsPinned(uses)).Select(uses => $"{workflow.RelativePath}: {uses}"))
            .ToList();

        // Então toda action externa aponta para um commit (SHA de 40 caracteres); docker:// por digest sha256
        unpinned.Should().BeEmpty("actions de terceiros ficam fixadas por SHA (ADR-0017, SPEC-0005 seção 5)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:UT-01")]
    public void Permissoes_sao_minimas_e_so_o_job_image_escreve_no_GHCR()
    {
        // Dado os workflows do repositório
        AssertContractWorkflowsExist();
        var violations = new List<string>();

        foreach (var workflow in Workflow.AllFileNames().Select(Workflow.Load))
        {
            // Então nenhum workflow roda em pull_request_target (segredos expostos a PRs de fork)
            if (workflow.Triggers.ContainsKey("pull_request_target"))
            {
                violations.Add($"{workflow.RelativePath}: usa pull_request_target");
            }

            // E o workflow não concede escrita
            var top = workflow.Root.Child("permissions");
            violations.AddRange(WriteScopes(top).Select(scope => $"{workflow.RelativePath}: permissions do workflow com {scope}"));

            foreach (var (jobId, job) in workflow.Jobs)
            {
                // E cada job tem permissões explícitas (as do job ou as do workflow) com contents: read
                var effective = job.Child("permissions") ?? top;
                if (effective.Text("contents") is not "read")
                {
                    violations.Add($"{workflow.RelativePath}: job '{jobId}' sem 'contents: read' explícito (no job ou no workflow)");
                }

                // E nenhum job escreve, exceto o image do ci.yml, que só ganha packages: write
                var allowed = string.Equals(workflow.RelativePath, $".github/workflows/{Workflow.CiFile}", StringComparison.Ordinal)
                    && string.Equals(jobId, "image", StringComparison.Ordinal)
                    ? "packages: write"
                    : null;
                violations.AddRange(WriteScopes(job.Child("permissions"))
                    .Where(scope => !string.Equals(scope, allowed, StringComparison.Ordinal))
                    .Select(scope => $"{workflow.RelativePath}: job '{jobId}' com {scope}"));
            }
        }

        violations.Should().BeEmpty("permissões mínimas por job (SPEC-0005 seção 5)");
    }

    internal static void AssertContractWorkflowsExist()
    {
        foreach (var name in ContractWorkflows)
        {
            Workflow.Exists(name).Should().BeTrue("o workflow .github/workflows/{0} existe (SPEC-0005, seção 6)", name);
        }
    }

    private static IEnumerable<string> WriteScopes(YamlDotNet.RepresentationModel.YamlNode? permissions)
    {
        if (permissions.Text() is { } shorthand)
        {
            return string.Equals(shorthand, "read-all", StringComparison.Ordinal) ? [] : [$"permissions: {shorthand}"];
        }

        return permissions.Entries()
            .Where(entry => string.Equals(entry.Value.Text(), "write", StringComparison.Ordinal))
            .Select(entry => $"{entry.Key}: write");
    }

    private static bool IsPinned(string uses)
    {
        if (uses.StartsWith("./", StringComparison.Ordinal))
        {
            return true;
        }

        return uses.StartsWith("docker://", StringComparison.Ordinal)
            ? DockerDigest().IsMatch(uses)
            : PinnedAction().IsMatch(uses);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_./-]+@[0-9a-f]{40}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex PinnedAction();

    [GeneratedRegex(@"@sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex DockerDigest();
}
