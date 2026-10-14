using System.Reflection;
using AwesomeAssertions;
using Juker.IntegrationTests.Harness;
using Juker.IntegrationTests.Tooling;
using Xunit;

namespace Juker.IntegrationTests.Testing;

/// <summary>
/// SPEC-0025:CT-01 — com <c>JUKER_E2E_BASE_URL</c>, o comando de smoke lista só os testes <c>Smoke</c> e os executa
/// contra a URL, sem subir o AppHost e sem chamar <c>/_test/*</c>. Roda um <c>dotnet test</c> aninhado sobre o build
/// atual, por isso fica na coleção serializada <see cref="NestedTestRun"/> (SPEC-0027).
/// </summary>
[Trait("Category", "Integration")]
[Collection(NestedTestRun.Name)]
public sealed class SmokeRunTests
{
    private const string SmokeCategory = "Smoke";
    private const string BaseUrlVariable = "JUKER_E2E_BASE_URL";
    private const string TestApiPrefix = "/_test";

    // smoke_test do contrato (SPEC-0025, seção 6)
    private const string SmokeCommand = "dotnet test --project tests/Juker.E2ETests -c Release --filter-trait \"Category=Smoke\"";

    // Docker inalcançável no processo filho: se a execução tentasse subir o AppHost (Postgres em container), o host
    // nunca ficaria pronto e a execução estouraria o prazo em vez de terminar com sucesso.
    private const string DockerHostVariable = "DOCKER_HOST";
    private const string UnreachableDocker = "tcp://127.0.0.1:9";
    private const string NestedRunTimeout = "5m";

    [Fact(Timeout = 600_000)]
    [Trait("Spec", "SPEC-0025:CT-01")]
    public async Task Smoke_com_JUKER_E2E_BASE_URL_lista_so_os_Smoke_e_roda_contra_a_URL_sem_AppHost_nem_API_de_teste()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado um servidor local de teste no papel do ambiente implantado, apontado por JUKER_E2E_BASE_URL
        await using var server = await RecordingServer.StartAsync(cancellationToken);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { [BaseUrlVariable] = server.BaseUrl.ToString() };

        // Quando o comando de smoke lista os testes (sobre o build atual) e, para comparar, sem o filtro
        var filtered = await ListAsync(Arguments(SmokeCommand), environment, cancellationToken);
        var unfilteredArguments = Arguments(SmokeCommand).ToList();
        unfilteredArguments.RemoveRange(unfilteredArguments.IndexOf("--filter-trait"), 2);
        var all = await ListAsync(unfilteredArguments, environment, cancellationToken);

        // Então lista só os testes Smoke (e existe algum), entre outros testes de outras categorias no projeto
        var smoke = DiscoveredTests.WithCategory(all.Assemblies, all.Tests, SmokeCategory);
        smoke.Should().NotBeEmpty("o Juker.E2ETests tem testes Smoke");
        all.Tests.Should().HaveCountGreaterThan(smoke.Count, "o Juker.E2ETests tem testes de outras categorias");
        filtered.Tests.Should().BeEquivalentTo(smoke);

        // Quando o comando de smoke executa, com o Docker inalcançável
        var runEnvironment = new Dictionary<string, string>(environment, StringComparer.Ordinal) { [DockerHostVariable] = UnreachableDocker };
        var run = await DotnetWithEnvironment.RunAsync([.. Arguments(SmokeCommand), "--timeout", NestedRunTimeout], runEnvironment, cancellationToken);

        // Então os Smoke passam contra a URL, sem subir o AppHost
        run.ExitCode.Should().Be(0, "os Smoke rodam contra JUKER_E2E_BASE_URL, sem AppHost. Saída:\n{0}", run.Output);
        server.Paths.Should().Contain("/health/ready", "os Smoke chamam o ambiente da URL");

        // E nada chama a API de teste
        server.Paths.Should().NotContain(
            path => path.StartsWith(TestApiPrefix, StringComparison.OrdinalIgnoreCase),
            "a API /_test/* não existe no ambiente implantado. Caminhos pedidos: {0}",
            string.Join(", ", server.Paths));
    }

    /// <summary>Os argumentos do comando, sem o <c>dotnet</c>, na configuração do build atual e sem recompilar.</summary>
    private static List<string> Arguments(string command)
    {
        var configuration = typeof(SmokeRunTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var arguments = SddCommands.Tokenize(command).Skip(1).ToList();
        arguments[arguments.IndexOf("-c") + 1] = configuration;
        arguments.Add("--no-build");
        return arguments;
    }

    private static async Task<(IReadOnlyList<string> Assemblies, IReadOnlyList<string> Tests)> ListAsync(
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        var result = await DotnetWithEnvironment.RunAsync([.. arguments, "--list-tests"], environment, cancellationToken);
        result.ExitCode.Should().Be(0, "a listagem deve terminar com sucesso. Saída:\n{0}", result.Output);
        return DiscoveredTests.Parse(result.Output);
    }
}
