using AwesomeAssertions;
using Juker.ArchitectureTests.Rules;
using Xunit;

namespace Juker.ArchitectureTests;

/// <summary>ARQ-06 (SPEC-0001, seção 3): a regra detecta a violação do fixture e aprova a solução real.</summary>
[Trait("Category", "Architecture")]
public sealed class ForbiddenDependencies
{
    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Sem_MediatR_MassTransit_nem_FluentAssertions__detecta_a_violacao_do_fixture()
    {
        // Quando a regra avalia o assembly de fixture (MediatR, MassTransit e FluentAssertions simulados por namespace)
        var violations = new ForbiddenDependenciesRule().Evaluate(ViolationsFixture.Subject());

        // Então aponta cada tipo Juker que depende de uma das três bibliotecas proibidas
        violations.Should().OnlyContain(violation => violation.Rule == "ARQ-06");
        violations.Select(violation => violation.Offender)
            .Should().Contain("Juker.Modules.Venues.Features.Register.RegisterVenueRequest")
            .And.Contain("Juker.Modules.Queue.Infrastructure.QueueEventPublisher")
            .And.Contain("Juker.Modules.Catalog.Infrastructure.SongAssertions");
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Sem_MediatR_MassTransit_nem_FluentAssertions__solucao_real_respeita_a_regra()
    {
        // Dado os assemblies reais da solução
        var subject = ArchitectureSubject.Solution();
        SolutionScope.ShouldCoverTheSolution(subject);

        // Quando a regra avalia, então não há violações
        var violations = new ForbiddenDependenciesRule().Evaluate(subject);
        violations.Should().BeEmpty();
    }
}
