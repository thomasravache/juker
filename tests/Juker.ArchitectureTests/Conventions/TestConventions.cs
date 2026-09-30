using System.Reflection;

namespace Juker.ArchitectureTests.Conventions;

#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

/// <summary>
/// Convenção de rastreabilidade (SPEC-0003, ADR-0011): todo <c>[Fact]</c>/<c>[Theory]</c> tem um trait
/// <c>Spec</c> no formato <c>SPEC-NNNN:(CH|UT|IT|CT|E2E)-NN</c> e um trait <c>Category</c> permitido
/// (<c>Unit</c>, <c>Integration</c>, <c>E2E</c>, <c>Smoke</c>, <c>Architecture</c>), no método ou na classe.
/// </summary>
public static class TestConventions
{
    /// <summary>Os assemblies de teste reais da solução (projetos de teste da Juker.slnx), sem fixtures.</summary>
    public static IReadOnlyList<Assembly> SolutionTestAssemblies() => throw new NotImplementedException();

    /// <summary>Todos os métodos de teste dos assemblies informados que violam a convenção.</summary>
    public static IReadOnlyList<ConventionViolation> FindViolations(IEnumerable<Assembly> testAssemblies) =>
        throw new NotImplementedException();
}
