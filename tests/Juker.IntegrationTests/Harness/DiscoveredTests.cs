using System.Reflection;
using System.Runtime.Loader;

namespace Juker.IntegrationTests.Harness;

/// <summary>
/// Interpreta a saída de <c>dotnet test --list-tests</c> (Microsoft.Testing.Platform) e resolve a Category de cada
/// teste listado refletindo os assemblies de teste num contexto de carga isolado.
/// Fora de um terminal, o cabeçalho "Discovered N tests in assembly" não separa os assemblies de forma confiável;
/// por isso os testes são identificados pelo nome completo e procurados em todos os assemblies descobertos.
/// </summary>
internal static class DiscoveredTests
{
    private const string DiscoveringFrom = "Discovering tests from ";
    private const string TraitAttribute = "Xunit.TraitAttribute";

    /// <summary>Os assemblies de teste descobertos e o nome completo (<c>Namespace.Classe.Método</c>) de cada teste listado.</summary>
    public static (IReadOnlyList<string> Assemblies, IReadOnlyList<string> Tests) Parse(string output)
    {
        var assemblies = new List<string>();
        var tests = new List<string>();
        foreach (var line in output.Split('\n').Select(raw => raw.TrimEnd('\r')))
        {
            var discovering = line.IndexOf(DiscoveringFrom, StringComparison.Ordinal);
            if (discovering >= 0)
            {
                var path = line[(discovering + DiscoveringFrom.Length)..];
                var suffix = path.LastIndexOf(" (", StringComparison.Ordinal);
                assemblies.Add(suffix >= 0 ? path[..suffix] : path);
                continue;
            }

            var name = line.Trim();
            if (line.StartsWith("  ", StringComparison.Ordinal) && name.Length > 0 && !name.Contains(' ', StringComparison.Ordinal))
            {
                var arguments = name.IndexOf('(', StringComparison.Ordinal);
                tests.Add(arguments >= 0 ? name[..arguments] : name);
            }
        }

        return (assemblies.Distinct(StringComparer.Ordinal).ToList(), tests.Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>Os testes (entre <paramref name="tests"/>) que têm o trait Category igual a <paramref name="category"/>.</summary>
    public static IReadOnlyList<string> WithCategory(IReadOnlyList<string> assemblies, IReadOnlyList<string> tests, string category)
    {
        var contexts = assemblies.Select(path => new IsolatedContext(path)).ToList();
        try
        {
            var loaded = contexts.Select(context => context.LoadFromAssemblyPath(context.MainAssemblyPath)).ToList();
            return tests
                .Where(test => Categories(loaded, test).Contains(category, StringComparer.Ordinal))
                .ToList();
        }
        finally
        {
            contexts.ForEach(context => context.Unload());
        }
    }

    private static IEnumerable<string> Categories(IReadOnlyList<Assembly> assemblies, string test)
    {
        var separator = test.LastIndexOf('.');
        var typeName = test[..separator];
        var methodName = test[(separator + 1)..];
        var type = assemblies.Select(assembly => assembly.GetType(typeName)).OfType<Type>().SingleOrDefault()
            ?? throw new InvalidOperationException($"Tipo do teste '{test}' não encontrado nos assemblies descobertos.");

        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .SelectMany(method => method.CustomAttributes)
            .Concat(type.CustomAttributes)
            .Where(attribute => string.Equals(attribute.AttributeType.FullName, TraitAttribute, StringComparison.Ordinal))
            .Where(attribute => attribute.ConstructorArguments is [{ Value: "Category" }, _])
            .Select(attribute => (string)attribute.ConstructorArguments[1].Value!);
    }

    private sealed class IsolatedContext(string assemblyPath) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

        public string MainAssemblyPath { get; } = assemblyPath;

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
