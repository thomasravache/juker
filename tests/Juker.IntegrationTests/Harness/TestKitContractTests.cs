using System.Reflection;
using AwesomeAssertions;
using Juker.IntegrationTests.Tooling;
using Juker.TestKit;
using Xunit;

namespace Juker.IntegrationTests.Harness;

[Trait("Category", "Integration")]
[Collection(NestedTestRun.Name)]
public sealed class TestKitContractTests
{
    private const string IntegrationCategory = "Integration";

    // Comandos do contrato (SPEC-0003, seção 6, contrato v2 — Microsoft.Testing.Platform)
    private const string TestCommand = "dotnet test --solution Juker.slnx -c Release";
    private const string TestIntegrationCommand = "dotnet test --solution Juker.slnx -c Release --filter-trait \"Category=Integration\"";
    private const string ArchTestCommand = "dotnet test --project tests/Juker.ArchitectureTests -c Release";

    [Fact(Timeout = 600_000)]
    [Trait("Spec", "SPEC-0003:CT-01")]
    public async Task Comando_test_integration_lista_somente_os_testes_Integration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado os comandos de teste do sdd-config.yml, iguais aos do contrato
        SddCommands.Read("test").Should().Be(TestCommand);
        SddCommands.Read("test_integration").Should().Be(TestIntegrationCommand);
        SddCommands.Read("arch_test").Should().Be(ArchTestCommand);

        // Quando o test_integration é executado com --list-tests (sobre o build atual) e, para comparar, sem o filtro
        var filtered = await ListAsync(SddCommands.Tokenize(TestIntegrationCommand), cancellationToken);
        var unfilteredArguments = SddCommands.Tokenize(TestIntegrationCommand).ToList();
        unfilteredArguments.RemoveRange(unfilteredArguments.IndexOf("--filter-trait"), 2);
        var all = await ListAsync(unfilteredArguments, cancellationToken);

        // Então lista exatamente os testes marcados com Category=Integration, e nenhum outro
        var expected = DiscoveredTests.WithCategory(all.Assemblies, all.Tests, IntegrationCategory);
        expected.Should().Contain($"{typeof(TestKitContractTests).FullName}.{nameof(Comando_test_integration_lista_somente_os_testes_Integration)}");
        all.Tests.Should().HaveCountGreaterThan(expected.Count, "há testes de outras categorias na solução");

        filtered.Tests.Should().BeEquivalentTo(expected);
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:CT-01")]
    public void Api_publica_do_TestKit_bate_com_o_snapshot_aprovado()
    {
        // Dado o snapshot aprovado do contrato do Juker.TestKit
        var approved = File.ReadAllText(RepositoryPaths.Combine("tests", "Juker.IntegrationTests", "Harness", "Juker.TestKit.approved.txt"))
            .ReplaceLineEndings("\n");

        // Quando a API pública do assembly é extraída, então é idêntica ao snapshot
        PublicApiSnapshot.Render(typeof(PostgresFixture).Assembly).Should().Be(approved);
    }

    private static async Task<(IReadOnlyList<string> Assemblies, IReadOnlyList<string> Tests)> ListAsync(
        IReadOnlyList<string> command,
        CancellationToken cancellationToken)
    {
        // Mesma configuração do build em execução, sem recompilar (o teste roda dentro do próprio `dotnet test`)
        var configuration = typeof(TestKitContractTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var arguments = command.Skip(1).ToList();
        arguments[arguments.IndexOf("-c") + 1] = configuration;
        arguments.AddRange(["--no-build", "--list-tests"]);

        var result = await DotnetCli.RunAsync(arguments, cancellationToken);
        result.ExitCode.Should().Be(0, "a listagem deve terminar com sucesso. Saída:\n{0}", result.Output);
        return DiscoveredTests.Parse(result.Output);
    }
}
