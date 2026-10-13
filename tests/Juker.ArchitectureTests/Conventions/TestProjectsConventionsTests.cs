using AwesomeAssertions;
using Juker.ArchitectureTests.ConventionViolations;
using Juker.ArchitectureTests.Rules;
using Juker.ArchitectureTests.Violations;
using Xunit;

namespace Juker.ArchitectureTests.Conventions;

/// <summary>Convenções sobre os projetos de teste da solução (SPEC-0027).</summary>
[Trait("Category", "Unit")]
public sealed class TestProjectsConventionsTests
{
    private const string SolutionFile = "Juker.slnx";
    private const string BuildingBlocksUnitTests = "tests/Juker.BuildingBlocks.UnitTests/Juker.BuildingBlocks.UnitTests.csproj";
    private const string IntegrationTests = "tests/Juker.IntegrationTests/Juker.IntegrationTests.csproj";

    private static readonly string ArchitectureProject = RepositoryPaths.Combine("tests", "Juker.ArchitectureTests", "Juker.ArchitectureTests.csproj");

    private static readonly string FixtureDirectory = RepositoryPaths.Combine("tests", "Juker.ArchitectureTests", "Fixtures", "TestProjects");

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-02")]
    public void Todo_projeto_de_tests_da_solucao_e_ProjectReference_do_ArchitectureTests()
    {
        // Dado o Juker.slnx e o Juker.ArchitectureTests.csproj
        var solution = RepositoryPaths.Combine(SolutionFile);
        TestProjects.InSolution(solution).Should().Contain([BuildingBlocksUnitTests, IntegrationTests], "a convenção lê os projetos reais de tests/");

        // Quando a convenção compara os projetos de tests/ (sem fixtures) com as ProjectReference
        var missing = TestProjects.MissingReferences(solution, ArchitectureProject);

        // Então nenhum projeto falta
        missing.Should().BeEmpty(
            "todo projeto de tests/ precisa de ProjectReference (ReferenceOutputAssembly=false) no Juker.ArchitectureTests.csproj. Faltam: {0}",
            string.Join(", ", missing));
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-02")]
    public void Projeto_de_tests_sem_ProjectReference_no_ArchitectureTests_e_listado()
    {
        // Dado uma solução de exemplo em que um projeto de tests/ não é referenciado pelo projeto de arquitetura
        var solution = Path.Combine(FixtureDirectory, "MissingReference.slnx");
        var architecture = Path.Combine(FixtureDirectory, "tests", "Juker.ArchitectureTests", "Juker.ArchitectureTests.csproj");

        // Quando a convenção compara
        var missing = TestProjects.MissingReferences(solution, architecture);

        // Então lista exatamente o projeto ausente (nem fixtures, nem src/, nem o próprio projeto de arquitetura)
        missing.Should().Equal("tests/Juker.Missing.UnitTests/Juker.Missing.UnitTests.csproj");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-03")]
    public void Regra_de_zero_testes_lista_projeto_sem_testes_sem_opt_in_e_projeto_com_testes_com_opt_in()
    {
        // Dado projetos de teste com e sem o opt-in, contados nos assemblies de fixture (com e sem [Fact]/[Theory])
        var withTests = TestConventions.CountTests(typeof(NonCompliantTests).Assembly);
        var withoutTests = TestConventions.CountTests(typeof(ViolationsAssemblyMarker).Assembly);
        withTests.Should().BeGreaterThan(0);
        withoutTests.Should().Be(0);

        TestProjectCount[] projects =
        [
            new("tests/Com.Testes.Sem.OptIn/Com.Testes.Sem.OptIn.csproj", AllowsZeroTests: false, withTests),
            new("tests/Sem.Testes.Com.OptIn/Sem.Testes.Com.OptIn.csproj", AllowsZeroTests: true, withoutTests),
            new("tests/Sem.Testes.Sem.OptIn/Sem.Testes.Sem.OptIn.csproj", AllowsZeroTests: false, withoutTests),
            new("tests/Com.Testes.Com.OptIn/Com.Testes.Com.OptIn.csproj", AllowsZeroTests: true, withTests),
        ];

        // Quando a regra avalia
        var violations = TestProjects.FindZeroTestsViolations(projects);

        // Então aponta exatamente o projeto que perdeu os testes e o que tem testes mas ainda declara o opt-in
        violations.Select(violation => violation.Project).Should().BeEquivalentTo(
            "tests/Sem.Testes.Sem.OptIn/Sem.Testes.Sem.OptIn.csproj",
            "tests/Com.Testes.Com.OptIn/Com.Testes.Com.OptIn.csproj");
        violations.Should().OnlyContain(violation => violation.Reason.Contains("JukerAllowZeroTests", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-03")]
    public void Projetos_de_teste_da_solucao_tem_testes_ou_o_opt_in_de_zero_testes_nunca_os_dois()
    {
        // Dado os projetos de teste (IsTestProject) de tests/ no Juker.slnx, sem fixtures
        var solution = RepositoryPaths.Combine(SolutionFile);
        var projects = TestProjects.InSolution(solution)
            .Select(project => (Relative: project, File: RepositoryPaths.Combine(project.Split('/'))))
            .Where(project => TestProjects.IsTestProject(project.File))
            .Select(project => new TestProjectCount(
                project.Relative,
                TestProjects.AllowsZeroTests(project.File),
                TestConventions.CountTests(SolutionAssemblies.LoadProject(project.File))))
            .ToList();
        projects.Select(project => project.Project).Should().Contain([BuildingBlocksUnitTests, IntegrationTests]);

        // Quando a regra de zero testes avalia cada um
        var violations = TestProjects.FindZeroTestsViolations(projects);

        // Então nenhum projeto sem JukerAllowZeroTests está vazio e nenhum com JukerAllowZeroTests=true tem testes
        violations.Should().BeEmpty(
            "a regra de zero testes vale para todo projeto de teste. Fora da regra:\n{0}",
            string.Join('\n', violations.Select(violation => $"{violation.Project}: {violation.Reason}")));
    }
}
