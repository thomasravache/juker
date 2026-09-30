using AwesomeAssertions;
using Xunit;

namespace Juker.IntegrationTests.Tooling;

[Trait("Category", "Integration")]
public sealed class SyncOverAsyncViolationBuildTests
{
    private const string FixtureRelativePath = "tests/fixtures/SyncOverAsyncViolation";
    private const string ConfiguredFixtureRelativePath = "tests/fixtures/SyncOverAsyncViolation/Configured";

    // Um arquivo por forma de awaiter configurado (SPEC-0028:IT-01).
    private static readonly string[] ConfiguredForms =
    [
        "ConfiguredTaskViolation.cs",
        "ConfiguredTaskOfTViolation.cs",
        "ConfiguredValueTaskViolation.cs",
        "ConfiguredValueTaskOfTViolation.cs",
    ];

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

    [Fact(Timeout = 600_000)]
    [Trait("Spec", "SPEC-0028:IT-01")]
    public async Task Build_falha_com_RS0030_em_cada_forma_de_awaiter_configurado()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado o fixture irmão com ConfigureAwait(false).GetAwaiter().GetResult() em Task, Task<T>, ValueTask e ValueTask<T>
        // Quando o fixture é compilado
        var result = await DotnetCli.RunAsync(["build", ConfiguredFixtureRelativePath], cancellationToken);

        // Então o build falha
        result.ExitCode.Should().NotBe(0, "GetResult() de awaiter configurado é bloqueio síncrono proibido fora do Program/CLI. Saída:\n{0}", result.Output);

        // E cada forma é rejeitada com RS0030 no próprio arquivo (ADR-0014, regra 3)
        var lines = result.Output.Split('\n');
        foreach (var form in ConfiguredForms)
        {
            lines.Should().Contain(
                line => line.Contains(form + "(", StringComparison.Ordinal) && line.Contains("error RS0030", StringComparison.Ordinal),
                "{0} deve ter RS0030. Saída:\n{1}",
                form,
                result.Output);
        }
    }
}
