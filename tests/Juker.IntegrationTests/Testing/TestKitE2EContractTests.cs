using System.Text.RegularExpressions;
using AwesomeAssertions;
using Juker.IntegrationTests.Harness;
using Juker.IntegrationTests.Tooling;
using Xunit;

namespace Juker.IntegrationTests.Testing;

/// <summary>
/// SPEC-0025:CT-01 — o contrato do TestKit de E2E, consumido pela SPEC-0005 e por todas as specs com E2E: a API
/// pública do <c>Juker.TestKit.E2E</c> e os comandos <c>test_e2e</c>/<c>smoke_test</c> do <c>sdd-config.yml</c>.
/// O snapshot aprovado foi escrito a partir da seção 6 da spec (contrato v3); qualquer diferença exige emenda.
/// </summary>
[Trait("Category", "Integration")]
public sealed partial class TestKitE2EContractTests
{
    // Comandos do contrato (SPEC-0025, seção 6)
    private const string TestE2ECommand = "dotnet test --project tests/Juker.E2ETests -c Release";
    private const string SmokeTestCommand = "dotnet test --project tests/Juker.E2ETests -c Release --filter-trait \"Category=Smoke\"";

    [Fact]
    [Trait("Spec", "SPEC-0025:CT-01")]
    public void Api_publica_do_TestKit_E2E_bate_com_o_snapshot_aprovado()
    {
        // Dado o snapshot aprovado do contrato do Juker.TestKit.E2E
        var approved = File.ReadAllText(RepositoryPaths.Combine("tests", "Juker.IntegrationTests", "Testing", "Juker.TestKit.E2E.approved.txt"))
            .ReplaceLineEndings("\n");

        // Quando a API pública do assembly é extraída, então é idêntica ao snapshot
        E2ETestsOutput.ReadTestKitE2E(PublicApiSnapshot.Render).Should().Be(approved);
    }

    [Fact]
    [Trait("Spec", "SPEC-0025:CT-01")]
    public void Comandos_test_e2e_e_smoke_test_do_sdd_config_batem_com_o_contrato()
    {
        // Dado o docs/specs/sdd-config.yml, quando os comandos de E2E são lidos, então são os do contrato
        ReadCommand("test_e2e").Should().Be(TestE2ECommand);
        ReadCommand("smoke_test").Should().Be(SmokeTestCommand);
    }

    // Como o SddCommands, mas aceitando dígitos na chave (test_e2e): o valor entre aspas, sem o comentário da linha.
    private static string? ReadCommand(string key) =>
        File.ReadLines(RepositoryPaths.Combine("docs", "specs", "sdd-config.yml"))
            .Select(line => CommandLine().Match(line))
            .Where(match => match.Success && string.Equals(match.Groups["key"].Value, key, StringComparison.Ordinal))
            .Select(match => match.Groups["value"].Value.Replace("\\\"", "\"", StringComparison.Ordinal))
            .FirstOrDefault();

    [GeneratedRegex("""^(?<key>[a-z0-9_]+):\s*"(?<value>(?:[^"\\]|\\.)*)"(?:\s*#.*)?$""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex CommandLine();
}
