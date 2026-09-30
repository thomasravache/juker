using AwesomeAssertions;
using Juker.ArchitectureTests.Rules;
using Xunit;

namespace Juker.ArchitectureTests;

/// <summary>ARQ-02 (SPEC-0001, seção 3): a regra detecta a violação do fixture e aprova a solução real.</summary>
[Trait("Category", "Architecture")]
public sealed class ContractsAreLeaves
{
    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Contracts_nao_referencia_internos_de_modulo__detecta_a_violacao_do_fixture()
    {
        // Quando a regra avalia o assembly de fixture
        var violations = new ContractsAreLeavesRule().Evaluate(ViolationsFixture.Subject());

        // Então aponta exatamente os tipos do .Contracts que dependem de um interno de módulo ou do
        // Juker.Infrastructure (regra estrita: só SharedKernel e outros .Contracts), e não o SessionStarted
        violations.Should().OnlyContain(violation => violation.Rule == "ARQ-02");
        violations.Select(violation => violation.Offender).Distinct(StringComparer.Ordinal)
            .Should().BeEquivalentTo(
                "Juker.Modules.Sessions.Contracts.SessionDto",
                "Juker.Modules.Sessions.Contracts.SessionClosedEvent");
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Contracts_nao_referencia_internos_de_modulo__solucao_real_respeita_a_regra()
    {
        // Dado os assemblies reais da solução
        var subject = ArchitectureSubject.Solution();
        SolutionScope.ShouldCoverTheSolution(subject);

        // Quando a regra avalia, então não há violações
        var violations = new ContractsAreLeavesRule().Evaluate(subject);
        violations.Should().BeEmpty();
    }
}
