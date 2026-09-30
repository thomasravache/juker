using System.Reflection;

namespace Juker.ArchitectureTests.Rules;

#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

/// <summary>O que as regras avaliam: os assemblies de produção e os assemblies de teste.</summary>
public sealed class ArchitectureSubject
{
    private ArchitectureSubject()
    {
    }

    /// <summary>Assemblies de produção (src/**).</summary>
    public IReadOnlyList<Assembly> ProductionAssemblies => throw new NotImplementedException();

    /// <summary>Assemblies dos projetos de teste (tests/**, sem os fixtures).</summary>
    public IReadOnlyList<Assembly> TestAssemblies => throw new NotImplementedException();

    /// <summary>Os assemblies reais da solução (Juker.slnx), sem os assemblies de fixture.</summary>
    public static ArchitectureSubject Solution() => throw new NotImplementedException();

    /// <summary>Um conjunto explícito de assemblies (usado com o fixture de violações do IT-02).</summary>
    public static ArchitectureSubject From(IEnumerable<Assembly> production, IEnumerable<Assembly> tests) =>
        throw new NotImplementedException();
}
