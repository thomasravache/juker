using AwesomeAssertions;
using Xunit;

namespace Juker.IntegrationTests.Tooling;

[Trait("Category", "Integration")]
public sealed class SyncOverAsyncViolationBuildTests
{
    private const string FixtureRelativePath = "tests/fixtures/SyncOverAsyncViolation";

    [Fact(Timeout = 600_000)]
    [Trait("Spec", "SPEC-0026:IT-01")]
    public async Task Build_falha_com_RS0030_quando_ha_bloqueio_sincrono_via_GetAwaiter_GetResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado o fixture fora da solução, com os props da raiz, contendo Task.CompletedTask.GetAwaiter().GetResult()
        // Quando o fixture é compilado
        var result = await DotnetCli.RunAsync(["build", FixtureRelativePath], cancellationToken);

        // Então o build falha com RS0030 (awaiter banido no BannedSymbols.txt, ADR-0014 regra 3)
        result.ExitCode.Should().NotBe(0, "GetAwaiter().GetResult() é bloqueio síncrono proibido fora do Program/CLI. Saída:\n{0}", result.Output);
        result.Output.Should().Contain("RS0030");
        result.Output.Should().Contain("GetResult");
    }
}
