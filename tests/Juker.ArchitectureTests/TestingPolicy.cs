using AwesomeAssertions;
using Juker.ArchitectureTests.Rules;
using Xunit;

namespace Juker.ArchitectureTests;

/// <summary>ARQ-08 (SPEC-0001, seção 3): a regra detecta a violação do fixture e aprova a solução real.</summary>
[Trait("Category", "Architecture")]
public sealed class TestingPolicy
{
    private const string InMemoryPackage = "Microsoft.EntityFrameworkCore.InMemory";
    private const string FixtureAssembly = "Juker.ArchitectureTests.Violations";
    private const string TestServerControl = "Juker.IntegrationTests.Realtime.TestServerClientSetup";
    private const string SubstituteOffender = "Juker.Modules.Catalog.UnitTests.SubstitutedHttpMessageHandler";
    private const string MoqOffender = "Juker.Modules.Catalog.UnitTests.MockedHttpMessageHandler";
    private const string SubstituteControl = "Juker.Modules.Catalog.UnitTests.SubstitutedDisposable";

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Testes_sem_EF_Core_InMemory_Sqlite_nem_mocks_de_HttpMessageHandler__detecta_a_violacao_do_fixture()
    {
        // Quando a regra avalia o assembly de fixture como projeto de teste
        var violations = new TestingPolicyRule().Evaluate(ViolationsFixture.Subject());

        // Então aponta exatamente o uso do EF Core InMemory, o mock de HttpMessageHandler escrito à mão e os mocks
        // com NSubstitute e Moq (SPEC-0027), e não o cliente que usa server.CreateHandler() do TestServer (ADR-0012,
        // regra 9). Além dos tipos, só é aceita a entrada do próprio projeto (assembly) que referencia o InMemory.
        violations.Should().OnlyContain(violation => violation.Rule == "ARQ-08");
        violations.Should().Contain(violation =>
            violation.Offender.Contains(InMemoryPackage, StringComparison.Ordinal)
            || violation.Reason.Contains(InMemoryPackage, StringComparison.Ordinal));
        violations.Select(violation => violation.Offender)
            .Where(offender => !string.Equals(offender, FixtureAssembly, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Should().BeEquivalentTo(
                "Juker.Modules.Queue.UnitTests.InMemoryQueueTests",
                "Juker.Modules.Catalog.UnitTests.StubHttpMessageHandler",
                SubstituteOffender,
                MoqOffender);
        violations.Should().NotContain(
            violation => violation.Offender.StartsWith(TestServerControl, StringComparison.Ordinal),
            "usar o handler do TestServer não é mock de HttpMessageHandler");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-01")]
    public void Mocks_de_HttpMessageHandler_com_NSubstitute_e_Moq_sao_apontados_e_mock_de_outro_tipo_nao()
    {
        // Dado o fixture com Substitute.For<HttpMessageHandler>(), new Mock<HttpMessageHandler>() e o controle
        // Substitute.For<IDisposable>() (tipos fake nos namespaces NSubstitute e Moq)
        var subject = ViolationsFixture.Subject();

        // Quando o ARQ-08 avalia
        var violations = new TestingPolicyRule().Evaluate(subject);

        // Então os dois mocks de HttpMessageHandler são apontados, como mock por biblioteca
        foreach (var offender in new[] { SubstituteOffender, MoqOffender })
        {
            violations.Should().ContainSingle(
                violation => string.Equals(violation.Offender, offender, StringComparison.Ordinal),
                "{0} mocka HttpMessageHandler com biblioteca de mock",
                offender);
            violations.Single(violation => string.Equals(violation.Offender, offender, StringComparison.Ordinal)).Reason
                .Should().Contain("System.Net.Http.HttpMessageHandler").And.Contain("biblioteca de mock");
        }

        // E o controle, que mocka outro tipo com a mesma biblioteca, não é
        violations.Should().NotContain(
            violation => violation.Offender.StartsWith(SubstituteControl, StringComparison.Ordinal),
            "mockar IDisposable não é mock de HttpMessageHandler");
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Testes_sem_EF_Core_InMemory_Sqlite_nem_mocks_de_HttpMessageHandler__solucao_real_respeita_a_regra()
    {
        // Dado os assemblies reais da solução
        var subject = ArchitectureSubject.Solution();
        SolutionScope.ShouldCoverTheSolution(subject);

        // Quando a regra avalia, então não há violações
        var violations = new TestingPolicyRule().Evaluate(subject);
        violations.Should().BeEmpty();
    }
}
