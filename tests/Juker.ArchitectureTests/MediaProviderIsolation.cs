using AwesomeAssertions;
using Juker.ArchitectureTests.Rules;
using Xunit;

namespace Juker.ArchitectureTests;

/// <summary>ARQ-07 (SPEC-0001, seção 3): a regra detecta a violação do fixture e aprova a solução real.</summary>
[Trait("Category", "Architecture")]
public sealed class MediaProviderIsolation
{
    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Tipos_YouTube_so_em_Catalog_Infrastructure_e_Web_Client_Tv__detecta_a_violacao_do_fixture()
    {
        // Quando a regra avalia o assembly de fixture
        var violations = new MediaProviderIsolationRule().Evaluate(ViolationsFixture.Subject());

        // Então aponta o tipo YouTube do módulo Queue e aceita os de Catalog.Infrastructure e Web.Client.Tv
        violations.Should().OnlyContain(violation => violation.Rule == "ARQ-07");
        violations.Select(violation => violation.Offender)
            .Should().Contain("Juker.Modules.Queue.YouTube.YouTubeVideoLookup")
            .And.NotContain("Juker.Modules.Catalog.Infrastructure.YouTube.YouTubeSearchClient")
            .And.NotContain("Juker.Web.Client.Tv.YouTube.YouTubePlayer");
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Tipos_YouTube_so_em_Catalog_Infrastructure_e_Web_Client_Tv__solucao_real_respeita_a_regra()
    {
        // Dado os assemblies reais da solução
        var subject = ArchitectureSubject.Solution();
        SolutionScope.ShouldCoverTheSolution(subject);

        // Quando a regra avalia, então não há violações
        var violations = new MediaProviderIsolationRule().Evaluate(subject);
        violations.Should().BeEmpty();
    }
}
