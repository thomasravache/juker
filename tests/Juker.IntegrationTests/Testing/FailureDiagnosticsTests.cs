using AwesomeAssertions;
using Juker.IntegrationTests.Harness;
using Juker.IntegrationTests.Tooling;
using Xunit;

namespace Juker.IntegrationTests.Testing;

/// <summary>
/// SPEC-0025:IT-03 — um E2E que falha deixa <c>trace.zip</c>, <c>video.webm</c> e <c>screenshot.png</c> em
/// <c>TestResults/playwright/&lt;teste&gt;/</c>, na saída do projeto de teste. O teste que falha de propósito fica no
/// fixture <c>tests/fixtures/E2EHarnessSelfTest</c>, fora da solução, executado aqui por um <c>dotnet test</c>
/// aninhado com um <c>--artifacts-path</c> temporário, na coleção serializada <see cref="NestedTestRun"/> (SPEC-0027).
/// </summary>
[Trait("Category", "Integration")]
[Collection(NestedTestRun.Name)]
public sealed class FailureDiagnosticsTests
{
    // MTP: código de saída quando pelo menos um teste falha (um build quebrado termina com 1).
    private const int TestFailureExitCode = 2;

    private const string FailingSample = "Exemplo_que_falha_de_proposito";
    private const string PassingSample = "Exemplo_que_passa";
    private static readonly string[] DiagnosticFiles = ["trace.zip", "video.webm", "screenshot.png"];

    [Fact(Timeout = 600_000)]
    [Trait("Spec", "SPEC-0025:IT-03")]
    public async Task Teste_E2E_que_falha_deixa_trace_video_e_screenshot_em_TestResults_playwright()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspace = Directory.CreateTempSubdirectory("juker-spec0025-it03-");
        try
        {
            // Dado o fixture com um teste que falha de propósito e outro que passa, ambos com a PlaywrightFixture
            var artifacts = Path.Combine(workspace.FullName, "artifacts");

            // Quando o fixture é executado isolado
            var run = await DotnetCli.RunAsync(
                [
                    "test",
                    "--project",
                    RepositoryPaths.Combine("tests", "fixtures", "E2EHarnessSelfTest", "E2EHarnessSelfTest.csproj"),
                    "-c",
                    "Release",
                    "--artifacts-path",
                    artifacts,
                ],
                cancellationToken);

            // Então a execução termina com falha de teste (não de build)
            run.ExitCode.Should().Be(TestFailureExitCode, "só o exemplo que falha de propósito deve falhar. Saída:\n{0}", run.Output);

            // E o teste que falhou deixa trace, vídeo e captura de tela na pasta dele em TestResults/playwright/
            var testDirectories = PlaywrightResultDirectories(artifacts);
            var failing = testDirectories.Where(directory => Path.GetFileName(directory).Contains(FailingSample, StringComparison.Ordinal)).ToList();
            failing.Should().ContainSingle(
                "TestResults/playwright/<teste>/ existe para o teste que falhou. Pastas encontradas: {0}",
                string.Join(", ", testDirectories));

            foreach (var file in DiagnosticFiles)
            {
                var path = Path.Combine(failing[0], file);
                File.Exists(path).Should().BeTrue("{0} é gravado quando o teste falha", file);
                new FileInfo(path).Length.Should().BePositive("{0} não pode estar vazio", file);
            }

            // E o teste que passou não deixa nada
            testDirectories.Should().NotContain(
                directory => Path.GetFileName(directory).Contains(PassingSample, StringComparison.Ordinal),
                "o diagnóstico só é gravado quando o teste falha");
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    /// <summary>As pastas por teste em <c>…/TestResults/playwright/</c> sob a saída do build aninhado.</summary>
    private static List<string> PlaywrightResultDirectories(string artifacts) =>
        Directory.Exists(artifacts)
            ? Directory.EnumerateDirectories(artifacts, "playwright", SearchOption.AllDirectories)
                .Where(directory => string.Equals(Path.GetFileName(Path.GetDirectoryName(directory)), "TestResults", StringComparison.Ordinal))
                .SelectMany(Directory.EnumerateDirectories)
                .ToList()
            : [];
}
