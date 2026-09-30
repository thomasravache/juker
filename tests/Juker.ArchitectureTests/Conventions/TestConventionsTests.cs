using AwesomeAssertions;
using Juker.ArchitectureTests.ConventionViolations;
using Xunit;

namespace Juker.ArchitectureTests.Conventions;

[Trait("Category", "Unit")]
public sealed class TestConventionsTests
{
    private const string FixtureNamespace = "Juker.ArchitectureTests.ConventionViolations";

    private static readonly string[] NonCompliant =
    [
        $"{FixtureNamespace}.NonCompliantTests.Sem_spec",
        $"{FixtureNamespace}.NonCompliantTests.Sem_categoria",
        $"{FixtureNamespace}.NonCompliantTests.Spec_fora_do_formato",
        $"{FixtureNamespace}.NonCompliantTests.Spec_com_nivel_desconhecido",
        $"{FixtureNamespace}.NonCompliantTests.Categoria_nao_permitida",
        $"{FixtureNamespace}.NonCompliantTests.Theory_sem_traits",
    ];

    private static readonly string[] ExpectedTestAssemblies = ["Juker.ArchitectureTests", "Juker.IntegrationTests"];

    private static readonly string[] FixtureAssemblies = ["Juker.ArchitectureTests.Violations", "Juker.ArchitectureTests.ConventionViolations"];

    [Fact]
    [Trait("Spec", "SPEC-0003:UT-01")]
    public void Convencao_lista_exatamente_os_metodos_sem_Spec_ou_Category_validos()
    {
        // Dado um assembly com testes dentro e fora da convenção (Category na classe ou no método)
        var fixture = typeof(NonCompliantTests).Assembly;

        // Quando refletido pelo teste de convenção
        var violations = TestConventions.FindViolations([fixture]);

        // Então lista exatamente os métodos sem Spec no formato, sem Category ou com Category não permitida
        violations.Select(violation => violation.Method).Distinct(StringComparer.Ordinal)
            .Should().BeEquivalentTo(NonCompliant);
        violations.Should().OnlyContain(violation => !string.IsNullOrWhiteSpace(violation.Reason));
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:UT-01")]
    public void Todos_os_testes_da_solucao_seguem_a_convencao()
    {
        // Dado todos os assemblies de teste da solução (inclusive os testes da SPEC-0002), sem os fixtures
        var assemblies = TestConventions.SolutionTestAssemblies();
        assemblies.Select(assembly => assembly.GetName().Name)
            .Should().Contain(ExpectedTestAssemblies)
            .And.NotContain(FixtureAssemblies);

        // Quando refletidos, então nenhum método de teste viola a convenção
        var violations = TestConventions.FindViolations(assemblies);
        violations.Should().BeEmpty();
    }
}
