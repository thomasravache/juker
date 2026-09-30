using AwesomeAssertions;
using Juker.ArchitectureTests.Rules;
using Xunit;

namespace Juker.ArchitectureTests;

/// <summary>ARQ-04 (SPEC-0001, seção 3): a regra detecta a violação do fixture e aprova a solução real.</summary>
[Trait("Category", "Architecture")]
public sealed class DomainPurity
{
    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Domain_sem_EF_Core_ASP_NET_Core_nem_Infrastructure__detecta_a_violacao_do_fixture()
    {
        // Quando a regra avalia o assembly de fixture
        var violations = new DomainPurityRule().Evaluate(ViolationsFixture.Subject());

        // Então aponta o tipo de Domain/ que depende do EF Core, e não os tipos de Domain/ puros
        violations.Should().OnlyContain(violation => violation.Rule == "ARQ-04");
        violations.Select(violation => violation.Offender)
            .Should().Contain("Juker.Modules.Catalog.Domain.Song")
            .And.NotContain("Juker.Modules.Queue.Domain.QueueItem")
            .And.NotContain("Juker.Modules.Sessions.Domain.Session");
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Domain_sem_EF_Core_ASP_NET_Core_nem_Infrastructure__solucao_real_respeita_a_regra()
    {
        // Dado os assemblies reais da solução
        var subject = ArchitectureSubject.Solution();
        SolutionScope.ShouldCoverTheSolution(subject);

        // Quando a regra avalia, então não há violações
        var violations = new DomainPurityRule().Evaluate(subject);
        violations.Should().BeEmpty();
    }
}
