using AwesomeAssertions;
using Juker.ArchitectureTests.Rules;
using Xunit;

namespace Juker.ArchitectureTests;

/// <summary>ARQ-03 (SPEC-0001, seção 3): a regra detecta a violação do fixture e aprova a solução real.</summary>
[Trait("Category", "Architecture")]
public sealed class DbContextOwnership
{
    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Um_DbContext_por_modulo_nao_compartilhado__detecta_a_violacao_do_fixture()
    {
        // Quando a regra avalia o assembly de fixture
        var violations = new DbContextOwnershipRule().Evaluate(ViolationsFixture.Subject());

        // Então aponta o segundo DbContext do módulo Playback
        violations.Should().OnlyContain(violation => violation.Rule == "ARQ-03");
        violations.Select(violation => violation.Offender)
            .Should().Contain(offender =>
                offender == "Juker.Modules.Playback.Infrastructure.PlaybackDbContext"
                || offender == "Juker.Modules.Playback.Infrastructure.PlaybackReportingDbContext");
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Um_DbContext_por_modulo_nao_compartilhado__solucao_real_respeita_a_regra()
    {
        // Dado os assemblies reais da solução
        var subject = ArchitectureSubject.Solution();
        SolutionScope.ShouldCoverTheSolution(subject);

        // Quando a regra avalia, então não há violações
        var violations = new DbContextOwnershipRule().Evaluate(subject);
        violations.Should().BeEmpty();
    }
}
