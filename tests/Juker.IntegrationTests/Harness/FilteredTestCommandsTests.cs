using System.Reflection;
using AwesomeAssertions;
using Juker.IntegrationTests.Tooling;
using Xunit;

namespace Juker.IntegrationTests.Harness;

[Trait("Category", "Integration")]
public sealed class FilteredTestCommandsTests
{
    // test_integration (SPEC-0003) e o comando do job unit (SPEC-0005): cada um esvazia algum projeto de teste.
    private const string TestIntegrationCommand = "dotnet test --solution Juker.slnx -c Release --filter-trait \"Category=Integration\"";
    private const string UnitCommand = "dotnet test --solution Juker.slnx -c Release --filter-trait \"Category=Unit\"";

    // Desliga o opt-in em todos os projetos (propriedade global vence a do .csproj): o estado da SPEC-0027 em que
    // nenhum projeto com testes declara JukerAllowZeroTests, e a execução filtrada ainda precisa aceitar zero testes.
    private const string WithoutOptIn = "-p:JukerAllowZeroTests=false";

    [Theory(Timeout = 600_000)]
    [InlineData(TestIntegrationCommand, false)]
    [InlineData(UnitCommand, false)]
    [InlineData(TestIntegrationCommand, true)]
    [InlineData(UnitCommand, true)]
    [Trait("Spec", "SPEC-0027:IT-03")]
    public async Task Comando_filtrado_por_categoria_termina_com_sucesso_mesmo_com_projetos_vazios_sob_o_filtro(string command, bool withoutOptIn)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado o comando filtrado, sobre o build atual, com os .csproj como estão ou sem o opt-in JukerAllowZeroTests
        // em nenhum projeto. O --list-tests passa pela mesma regra do código de saída 8 (nenhum teste no projeto) e
        // pela validação das opções (código 5 com --ignore-exit-code duplicado) sem executar os testes, o que evita
        // rodar esta suíte dentro dela mesma
        var configuration = typeof(FilteredTestCommandsTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var arguments = SddCommands.Tokenize(command).Skip(1).ToList();
        arguments[arguments.IndexOf("-c") + 1] = configuration;
        arguments.AddRange(["--no-build", "--list-tests"]);
        if (withoutOptIn)
        {
            arguments.Add(WithoutOptIn);
        }

        // Quando o comando roda sobre a solução inteira
        var result = await DotnetCli.RunAsync(arguments, cancellationToken);

        // Então termina com sucesso: sem o código 8 dos projetos que o filtro esvazia nem o 5 de opção duplicada
        result.ExitCode.Should().Be(0, "o comando filtrado deve terminar com sucesso. Saída:\n{0}", result.Output);
        DiscoveredTests.Parse(result.Output).Tests.Should().NotBeEmpty("o filtro seleciona testes em algum projeto");
    }
}
