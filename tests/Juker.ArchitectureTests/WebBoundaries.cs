using AwesomeAssertions;
using Juker.ArchitectureTests.Rules;
using Xunit;

namespace Juker.ArchitectureTests;

/// <summary>ARQ-05 (SPEC-0001, seção 3): a regra detecta a violação do fixture e aprova a solução real.</summary>
[Trait("Category", "Architecture")]
public sealed class WebBoundaries
{
    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Web_so_referencia_o_Contracts_dos_modulos__detecta_a_violacao_do_fixture()
    {
        // Quando a regra avalia o assembly de fixture
        var violations = new WebBoundariesRule().Evaluate(ViolationsFixture.Subject());

        // Então aponta exatamente o componente do Web que usa um interno do módulo Queue (não o QueueSummary,
        // que usa só o .Contracts e o SharedKernel)
        violations.Should().OnlyContain(violation => violation.Rule == "ARQ-05");
        violations.Select(violation => violation.Offender).Distinct(StringComparer.Ordinal)
            .Should().BeEquivalentTo("Juker.Web.Components.QueuePanel");
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Web_so_referencia_o_Contracts_dos_modulos__solucao_real_respeita_a_regra()
    {
        // Dado os assemblies reais da solução
        var subject = ArchitectureSubject.Solution();
        SolutionScope.ShouldCoverTheSolution(subject);

        // Quando a regra avalia, então não há violações
        var violations = new WebBoundariesRule().Evaluate(subject);
        violations.Should().BeEmpty();
    }
}
