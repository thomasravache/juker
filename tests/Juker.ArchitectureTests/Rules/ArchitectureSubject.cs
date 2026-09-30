using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Assembly = System.Reflection.Assembly;
using Type = System.Type;

namespace Juker.ArchitectureTests.Rules;

/// <summary>O que as regras avaliam: os assemblies de produção e os assemblies de teste.</summary>
public sealed class ArchitectureSubject
{
    private static readonly Lazy<ArchitectureSubject> SolutionSubject =
        new(() => new ArchitectureSubject(SolutionAssemblies.Production, SolutionAssemblies.Tests));

    private readonly Lazy<Architecture> _architecture;
    private readonly HashSet<string> _productionNames;
    private readonly HashSet<string> _testNames;

    private ArchitectureSubject(IReadOnlyList<Assembly> production, IReadOnlyList<Assembly> tests)
    {
        ProductionAssemblies = production;
        TestAssemblies = tests;
        _productionNames = production.Select(Name).ToHashSet(StringComparer.Ordinal);
        _testNames = tests.Select(Name).ToHashSet(StringComparer.Ordinal);
        _architecture = new(() => new ArchLoader()
            .LoadAssemblies([.. production.Concat(tests).DistinctBy(Name, StringComparer.Ordinal)])
            .Build());
    }

    /// <summary>Assemblies de produção (src/**).</summary>
    public IReadOnlyList<Assembly> ProductionAssemblies { get; }

    /// <summary>Assemblies dos projetos de teste (tests/**, sem os fixtures).</summary>
    public IReadOnlyList<Assembly> TestAssemblies { get; }

    /// <summary>Tipos (ArchUnitNET) declarados nos assemblies de produção.</summary>
    internal IEnumerable<IType> ProductionTypes => TypesOf(_productionNames);

    /// <summary>Tipos (ArchUnitNET) declarados nos assemblies de teste.</summary>
    internal IEnumerable<IType> TestTypes => TypesOf(_testNames);

    /// <summary>Tipos (ArchUnitNET) de produção e de teste, sem repetição.</summary>
    internal IEnumerable<IType> AllTypes => TypesOf([.. _productionNames, .. _testNames]);

    /// <summary>Os assemblies reais da solução (Juker.slnx), sem os assemblies de fixture.</summary>
    public static ArchitectureSubject Solution() => SolutionSubject.Value;

    /// <summary>Um conjunto explícito de assemblies (usado com o fixture de violações do IT-02).</summary>
    public static ArchitectureSubject From(IEnumerable<Assembly> production, IEnumerable<Assembly> tests)
    {
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(tests);
        return new ArchitectureSubject([.. production], [.. tests]);
    }

    /// <summary>Tipos por reflexão dos assemblies informados (tipos que não carregam são ignorados).</summary>
    internal static IEnumerable<Type> ReflectedTypes(IEnumerable<Assembly> assemblies) =>
        assemblies.SelectMany(assembly =>
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.OfType<Type>();
            }
        });

    private IEnumerable<IType> TypesOf(HashSet<string> assemblyNames) =>
        _architecture.Value.Types.Where(type => assemblyNames.Contains(TypeNames.AssemblyName(type)));

    private static string Name(Assembly assembly) => assembly.GetName().Name!;
}
