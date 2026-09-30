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

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-02")]
    public void Testes_sem_EF_Core_InMemory_Sqlite_nem_mocks_de_HttpMessageHandler__detecta_a_violacao_do_fixture()
    {
        // Quando a regra avalia o assembly de fixture como projeto de teste
        var violations = new TestingPolicyRule().Evaluate(ViolationsFixture.Subject());

        // Então aponta exatamente o uso do EF Core InMemory e o mock de HttpMessageHandler escrito à mão, e não o
        // cliente que usa server.CreateHandler() do TestServer (ADR-0012, regra 9). Além dos tipos, só é aceita a
        // entrada do próprio projeto (assembly) que referencia o InMemory.
        violations.Should().OnlyContain(violation => violation.Rule == "ARQ-08");
        violations.Should().Contain(violation =>
            violation.Offender.Contains(InMemoryPackage, StringComparison.Ordinal)
            || violation.Reason.Contains(InMemoryPackage, StringComparison.Ordinal));
        violations.Select(violation => violation.Offender)
            .Where(offender => !string.Equals(offender, FixtureAssembly, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Should().BeEquivalentTo(
                "Juker.Modules.Queue.UnitTests.InMemoryQueueTests",
                "Juker.Modules.Catalog.UnitTests.StubHttpMessageHandler");
        violations.Should().NotContain(
            violation => violation.Offender.StartsWith(TestServerControl, StringComparison.Ordinal),
            "usar o handler do TestServer não é mock de HttpMessageHandler");
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
