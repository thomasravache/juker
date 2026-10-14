using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Juker.CiTests.Workflows;

/// <summary>
/// SPEC-0005:CT-01 — o contrato do <c>ci.yml</c> (SPEC-0005, seção 6, contrato v3): gatilhos, nomes dos jobs (os
/// checks obrigatórios da proteção da <c>main</c>), dependências, comandos, artefatos, o output <c>image_digest</c> e a
/// tag <c>ghcr.io/&lt;owner&gt;/juker:&lt;sha&gt;</c> que o <c>deploy.yml</c> (SPEC-0006) consome, e as regras de custo
/// da emenda v3 (concurrency com cancel-in-progress, artefatos por 7 dias e cache do NuGet e dos navegadores).
/// </summary>
/// <remarks>
/// A referência da imagem fica escrita por extenso no job <c>image</c>
/// (<c>ghcr.io/${{ github.repository_owner }}/juker:${{ github.sha }}</c>), para que a SPEC-0006 e o leitor a achem
/// no YAML; um nome de imagem montado só em tempo de execução não satisfaz o contrato.
/// </remarks>
[Trait("Category", "Unit")]
public sealed partial class CiWorkflowContractTests
{
    private const int ArtifactRetentionDays = 7;

    /// <summary>Os checks obrigatórios do contrato, na ordem da seção 6.</summary>
    public static readonly TheoryData<string> RequiredJobs =
        ["build", "lint", "unit", "integration", "architecture", "e2e", "security", "coverage", "sdd", "image"];

    /// <summary>Jobs que publicam os resultados de teste (TRX) como <c>test-results-&lt;job&gt;</c>.</summary>
    public static readonly TheoryData<string> TestJobs = ["unit", "integration", "architecture", "e2e"];

    /// <summary>Trechos de comando de cada job (comparados sem aspas e com espaços colapsados), da seção 6.</summary>
    public static readonly TheoryData<string, string> JobCommands = new()
    {
        { "build", "dotnet build Juker.slnx -c Release -warnaserror" },
        { "lint", "dotnet format Juker.slnx --verify-no-changes" },
        { "unit", "dotnet test --solution Juker.slnx -c Release" },
        { "unit", "--filter-trait Category=Unit" },
        { "integration", "dotnet test --solution Juker.slnx -c Release" },
        { "integration", "--filter-trait Category=Integration" },
        { "architecture", "dotnet test --project tests/Juker.ArchitectureTests -c Release" },
        { "e2e", "install --with-deps chromium webkit" },
        { "e2e", "dotnet test --project tests/Juker.E2ETests -c Release" },
        { "security", "--vulnerable" },
        { "security", "--include-transitive" },
        { "coverage", "reportgenerator" },
        { "coverage", "diff-cover" },
        { "coverage", "--compare-branch=origin/main" },
        { "coverage", "--fail-under=80" },
        { "sdd", "spec_graph.py validate --exclude-drafts" },
        { "sdd", "spec_graph.py pr-check" },
    };

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Gatilhos_sao_pull_request_e_push_na_main()
    {
        // Dado o ci.yml
        var ci = LoadCi();

        // Então ele roda em todo PR para a main e em todo push na main
        ci.Triggers.Keys.Should().Contain(["pull_request", "push"]);
        ci.Triggers["pull_request"].Child("branches").Texts().Should().Equal("main");
        ci.Triggers["push"].Child("branches").Texts().Should().Equal("main");
    }

    [Theory]
    [MemberData(nameof(RequiredJobs))]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Job_obrigatorio_existe_com_o_nome_do_contrato(string jobId)
    {
        // Dado o ci.yml
        var jobs = LoadCi().Jobs;

        // Então o job existe e o nome do check é o id do contrato (é o nome exigido na proteção da main)
        jobs.Should().ContainKey(jobId);
        var displayName = jobs[jobId].Text("name");
        (displayName is null || string.Equals(displayName, jobId, StringComparison.Ordinal)).Should()
            .BeTrue("o check '{0}' não pode ganhar outro nome (name: '{1}'): a proteção da main exige o nome do contrato", jobId, displayName);
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Coverage_depende_de_unit_e_integration_e_image_de_todos_os_outros_jobs()
    {
        // Dado o ci.yml
        var jobs = LoadCi().Jobs;
        jobs.Should().ContainKeys("coverage", "image");

        // Então o coverage espera unit e integration, e o image espera todos os jobs de qualidade
        jobs["coverage"].Child("needs").Texts().Should().Contain(["unit", "integration"]);
        jobs["image"].Child("needs").Texts().Should()
            .Contain(["build", "lint", "unit", "integration", "architecture", "e2e", "security", "coverage", "sdd"]);
    }

    [Theory]
    [MemberData(nameof(JobCommands))]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Job_roda_o_comando_do_contrato(string jobId, string command)
    {
        // Dado o job do ci.yml
        var jobs = LoadCi().Jobs;
        jobs.Should().ContainKey(jobId);

        // Então os scripts do job contêm o comando da seção 6
        Workflow.RunText(jobs[jobId]).Should().Contain(Workflow.NormalizeCommand(command));
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Security_lista_pacotes_vulneraveis_e_roda_o_gitleaks()
    {
        // Dado o job security do ci.yml
        var jobs = LoadCi().Jobs;
        jobs.Should().ContainKey("security");
        var security = jobs["security"];

        // Então ele roda o dotnet list package --vulnerable e o gitleaks detect (pelo CLI ou pela action oficial)
        DotnetListVulnerable().IsMatch(Workflow.RunText(security)).Should()
            .BeTrue("o job security roda 'dotnet list [<solução>] package --vulnerable' (ou 'dotnet package list --vulnerable')");
        var gitleaks = Workflow.RunText(security).Contains("gitleaks detect", StringComparison.Ordinal)
            || Workflow.StepsUsing(security, "gitleaks/gitleaks-action").Any();
        gitleaks.Should().BeTrue("o job security roda o gitleaks detect");
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Sdd_passa_titulo_e_corpo_do_PR_ao_pr_check()
    {
        // Dado o job sdd do ci.yml
        var jobs = LoadCi().Jobs;
        jobs.Should().ContainKey("sdd");

        // Então o pr-check recebe PR_TITLE e PR_BODY
        jobs["sdd"].AllScalars().Should().Contain(["PR_TITLE", "PR_BODY"]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Image_publica_o_output_image_digest_e_a_tag_ghcr_com_o_sha()
    {
        // Dado o job image do ci.yml
        var jobs = LoadCi().Jobs;
        jobs.Should().ContainKey("image");
        var image = jobs["image"];

        // Então o job expõe o output image_digest a partir de um passo
        var digest = image.Mapping("outputs").Text("image_digest");
        digest.Should().NotBeNull("o job image expõe o output 'image_digest' (consumido pelo deploy.yml da SPEC-0006)");
        StepOutput().IsMatch(digest!).Should().BeTrue("image_digest vem do output de um passo (steps.<id>.outputs.<nome>), mas é '{0}'", digest);

        // E a imagem é ghcr.io/<owner>/juker:<sha>
        image.AllScalars().Any(value => ImageReference().IsMatch(value)).Should()
            .BeTrue("o job image usa a referência ghcr.io/<github.repository_owner>/juker:<github.sha> escrita por extenso");
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void E2e_publica_playwright_traces_so_quando_falha()
    {
        // Dado o job e2e do ci.yml
        var jobs = LoadCi().Jobs;
        jobs.Should().ContainKey("e2e");

        // Então um upload-artifact chamado playwright-traces, condicionado a failure(), leva TestResults/playwright
        var traces = Workflow.StepsUsing(jobs["e2e"], "actions/upload-artifact")
            .Where(step => string.Equals(step.Child("with").Text("name"), "playwright-traces", StringComparison.Ordinal))
            .ToList();
        traces.Should().ContainSingle("o e2e publica o artefato playwright-traces (ADR-0013)");
        traces[0].Text("if").Should().Contain("failure()");
        traces[0].Child("with").Text("path").Should().Contain("TestResults/playwright");
    }

    [Theory]
    [MemberData(nameof(TestJobs))]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Job_de_teste_publica_o_TRX_como_test_results(string jobId)
    {
        // Dado o job de teste do ci.yml
        var jobs = LoadCi().Jobs;
        jobs.Should().ContainKey(jobId);

        // Então ele publica o artefato test-results-<job>
        var names = Workflow.StepsUsing(jobs[jobId], "actions/upload-artifact").Select(step => step.Child("with").Text("name")).ToList();
        names.Should().Contain(name => string.Equals(name, $"test-results-{jobId}", StringComparison.Ordinal) || GitHubJobArtifact().IsMatch(name ?? string.Empty),
            "o job '{0}' publica 'test-results-{0}' (ou test-results-<github.job>)", jobId);
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Coverage_publica_o_coverage_report()
    {
        // Dado o job coverage do ci.yml
        var jobs = LoadCi().Jobs;
        jobs.Should().ContainKey("coverage");

        // Então ele publica o artefato coverage-report
        Workflow.StepsUsing(jobs["coverage"], "actions/upload-artifact").Select(step => step.Child("with").Text("name"))
            .Should().Contain("coverage-report");
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Concorrencia_cancela_a_execucao_anterior_da_mesma_branch_ou_PR()
    {
        // Dado o ci.yml (emenda v3: consumo dentro da cota gratuita)
        var concurrency = LoadCi().Root.Child("concurrency");

        // Então há um grupo por branch/PR e cancel-in-progress
        concurrency.Should().BeOfType<YamlMappingNode>("o ci.yml declara concurrency com group e cancel-in-progress");
        var group = concurrency.Text("group") ?? string.Empty;
        PerBranchGroup().IsMatch(group).Should()
            .BeTrue("o grupo separa as execuções por branch/PR (github.ref, github.head_ref ou o número do PR), mas é '{0}'", group);
        var cancel = concurrency.Text("cancel-in-progress");
        (string.Equals(cancel, "true", StringComparison.Ordinal) || Expression().IsMatch(cancel ?? string.Empty)).Should()
            .BeTrue("cancel-in-progress é true (ou uma expressão), mas é '{0}'", cancel);
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Todo_upload_de_artefato_fica_retido_por_7_dias()
    {
        // Dado o ci.yml e o mutation.yml
        WorkflowLintTests.AssertContractWorkflowsExist();
        var uploads = new[] { Workflow.CiFile, Workflow.MutationFile }
            .Select(Workflow.Load)
            .SelectMany(workflow => workflow.UploadSteps().Select(upload => (workflow.RelativePath, upload.Job, upload.Step)))
            .ToList();
        uploads.Should().NotBeEmpty();

        // Então todo upload-artifact declara retention-days: 7 (emenda v3, 500 MB de artefatos no GitHub Free)
        uploads
            .Where(upload => !string.Equals(upload.Step.Child("with").Text("retention-days"), ArtifactRetentionDays.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
            .Select(upload => $"{upload.RelativePath}: job '{upload.Job}', artefato '{upload.Step.Child("with").Text("name")}'")
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Cache_do_NuGet_e_dos_navegadores_usa_actions_cache()
    {
        // Dado o ci.yml (emenda v3)
        var jobs = LoadCi().Jobs;
        jobs.Should().ContainKey("e2e");

        // Então o actions/cache guarda os pacotes do NuGet e, no e2e, os navegadores do Playwright
        var cachedPaths = jobs.Values.SelectMany(job => CachePaths(job)).ToList();
        cachedPaths.Should().Contain(path => path.Contains(".nuget/packages", StringComparison.Ordinal), "o NuGet é restaurado do actions/cache");
        CachePaths(jobs["e2e"]).Should().Contain(path => path.Contains("ms-playwright", StringComparison.Ordinal), "os navegadores do Playwright vêm do actions/cache");
    }

    private static Workflow LoadCi()
    {
        Workflow.Exists(Workflow.CiFile).Should().BeTrue("o workflow .github/workflows/ci.yml existe (SPEC-0005, seção 6)");
        return Workflow.Load(Workflow.CiFile);
    }

    private static IEnumerable<string> CachePaths(YamlMappingNode job) =>
        Workflow.Steps(job)
            .Where(step => step.Text("uses") is { } uses && uses.StartsWith("actions/cache", StringComparison.OrdinalIgnoreCase))
            .Select(step => step.Child("with").Text("path"))
            .OfType<string>();

    [GeneratedRegex(@"^\$\{\{\s*steps\.[A-Za-z0-9_-]+\.outputs\.[A-Za-z0-9_-]+\s*\}\}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex StepOutput();

    [GeneratedRegex(@"ghcr\.io/\$\{\{\s*github\.repository_owner\s*\}\}/juker:\$\{\{\s*github\.sha\s*\}\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex ImageReference();

    [GeneratedRegex(@"dotnet (list( \S+)? package|package list)\b.*--vulnerable", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex DotnetListVulnerable();

    [GeneratedRegex(@"^test-results-\$\{\{\s*github\.job\s*\}\}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex GitHubJobArtifact();

    [GeneratedRegex(@"github\.ref\b|github\.head_ref|pull_request\.number", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex PerBranchGroup();

    [GeneratedRegex(@"^\$\{\{.+\}\}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex Expression();
}
