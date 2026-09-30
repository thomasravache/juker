using AwesomeAssertions;
using Juker.ArchitectureTests.Rules;
using Xunit;

namespace Juker.ArchitectureTests;

/// <summary>ARQ-01 (SPEC-0001, seção 3): a regra detecta a violação do fixture e aprova a solução real.</summary>
[Trait("Category", "Architecture")]
public sealed class ModuleBoundaries
{
    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Modulo_so_referencia_o_Contracts_de_outro_modulo__detecta_a_violacao_do_fixture()
    {
        // Quando a regra avalia o assembly de fixture
        var violations = new ModuleBoundariesRule().Evaluate(ViolationsFixture.Subject());

        // Então aponta o módulo Venues usando um interno do módulo Queue
        violations.Should().OnlyContain(violation => violation.Rule == "ARQ-01");
        violations.Select(violation => violation.Offender)
            .Should().Contain("Juker.Modules.Venues.Features.CheckIn.CheckInHandler")
            .And.NotContain("Juker.Modules.Venues.Features.CheckIn.QueueSnapshotReader", "usar o .Contracts de outro módulo é permitido");
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Modulo_so_referencia_o_Contracts_de_outro_modulo__solucao_real_respeita_a_regra()
    {
        // Dado os assemblies reais da solução
        var subject = ArchitectureSubject.Solution();
        SolutionScope.ShouldCoverTheSolution(subject);

        // Quando a regra avalia, então não há violações
        var violations = new ModuleBoundariesRule().Evaluate(subject);
        violations.Should().BeEmpty();
    }
}
