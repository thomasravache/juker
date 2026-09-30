using AwesomeAssertions;
using Juker.IntegrationTests.Tooling;
using Juker.TestKit;
using Xunit;

namespace Juker.IntegrationTests.Harness;

[Trait("Category", "Integration")]
public sealed class MigrationsGuardTests
{
    private const string FixtureAssembly = "Juker.IntegrationTests.PendingModelChanges";
    private const string FixtureContext = "Juker.IntegrationTests.PendingModelChanges.WidgetsDbContext";

    // Texto do próprio guard: garante que a falha é a detecção da mudança, não um erro ao instanciar o contexto.
    private const string GuardMessage = "mudanças no modelo sem migration";

    [Fact(Timeout = 600_000)]
    [Trait("Spec", "SPEC-0003:IT-05")]
    public async Task Guard_falha_citando_o_DbContext_com_propriedade_sem_migration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspace = Directory.CreateTempSubdirectory("juker-it05-");
        try
        {
            // Dado um DbContext de fixture com uma propriedade nova (Widget.Color) sem migration
            var artifacts = Path.Combine(workspace.FullName, "artifacts");
            var build = await DotnetCli.RunAsync(
                ["build", RepositoryPaths.Combine("tests", "Juker.IntegrationTests", "Fixtures", "PendingModelChanges"), "-c", "Release", "--artifacts-path", artifacts],
                cancellationToken);
            build.ExitCode.Should().Be(0, "o fixture deve compilar. Saída:\n{0}", build.Output);
            var executable = Directory.EnumerateFiles(artifacts, FixtureAssembly + ".dll", SearchOption.AllDirectories)
                .Single(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

            // Quando o MigrationsGuard roda no processo do fixture
            var run = await DotnetCli.RunAsync([executable], cancellationToken);

            // Então falha com a mensagem do guard citando o contexto
            run.ExitCode.Should().NotBe(0, "o guard deve falhar. Saída:\n{0}", run.Output);
            run.Output.Should().Contain(GuardMessage).And.Contain(FixtureContext);
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-05")]
    public void Guard_passa_para_os_DbContext_reais_da_solucao()
    {
        // Dado os DbContext reais da solução (alcançáveis a partir deste assembly de teste)
        // Quando o guard verifica, então não há mudanças de modelo sem migration
        var act = MigrationsGuard.AssertNoPendingModelChanges;
        act.Should().NotThrow();
    }
}
