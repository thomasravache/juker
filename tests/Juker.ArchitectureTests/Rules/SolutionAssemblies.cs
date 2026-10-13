using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;

namespace Juker.ArchitectureTests.Rules;

/// <summary>
/// Os assemblies compilados dos projetos da <c>Juker.slnx</c>: <c>src/**</c> (produção) e <c>tests/**</c> (teste).
/// Os fixtures ficam fora da solução e, portanto, fora daqui. O AppHost do Aspire (orquestração, sem código do
/// produto) também fica fora das regras.
/// </summary>
/// <remarks>
/// Assemblies que já estão na saída deste projeto são carregados normalmente; os demais (projetos de teste, que este
/// projeto só referencia para garantir o build) são lidos da saída do próprio projeto, na mesma configuração, num
/// contexto de carga com as dependências daquele projeto.
/// </remarks>
internal static class SolutionAssemblies
{
    private const string SolutionFile = "Juker.slnx";
    private const string AspireAppHostSdk = "Aspire.AppHost.Sdk";

    private static readonly Lazy<IReadOnlyList<Assembly>> ProductionAssemblies = new(() => Load("src"));
    private static readonly Lazy<IReadOnlyList<Assembly>> TestAssemblies = new(() => Load("tests"));

    public static IReadOnlyList<Assembly> Production => ProductionAssemblies.Value;

    public static IReadOnlyList<Assembly> Tests => TestAssemblies.Value;

    private static List<Assembly> Load(string topLevelDirectory)
    {
        var solution = XDocument.Load(RepositoryPaths.Combine(SolutionFile));
        return solution.Descendants("Project")
            .Select(project => (string?)project.Attribute("Path"))
            .OfType<string>()
            .Where(path => path.StartsWith(topLevelDirectory + "/", StringComparison.Ordinal))
            .Select(path => RepositoryPaths.Combine(path.Split('/')))
            .Where(project => !File.ReadAllText(project).Contains(AspireAppHostSdk, StringComparison.Ordinal))
            .Select(LoadProject)
            .ToList();
    }

    /// <summary>O assembly compilado de um projeto da solução (caminho absoluto do <c>.csproj</c>).</summary>
    internal static Assembly LoadProject(string projectFile)
    {
        var name = AssemblyName(projectFile);
        if (File.Exists(Path.Combine(AppContext.BaseDirectory, name + ".dll")))
        {
            return Assembly.Load(new AssemblyName(name));
        }

        // Mesmo layout de saída deste projeto (bin/<Configuração>/<TFM>/) aplicado ao projeto de destino.
        var thisProject = RepositoryPaths.Combine("tests", "Juker.ArchitectureTests");
        var outputLayout = Path.GetRelativePath(thisProject, AppContext.BaseDirectory);
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFile)!, outputLayout, name + ".dll"));
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Assembly '{name}' não encontrado em '{path}'. Referencie '{projectFile}' no Juker.ArchitectureTests.csproj " +
                "com ReferenceOutputAssembly=false para que o arch_test o compile.");
        }

        return new ProjectLoadContext(path).LoadFromAssemblyPath(path);
    }

    private static string AssemblyName(string projectFile)
    {
        var declared = XDocument.Load(projectFile).Descendants("AssemblyName").Select(element => element.Value).FirstOrDefault();
        return string.IsNullOrWhiteSpace(declared) ? Path.GetFileNameWithoutExtension(projectFile) : declared;
    }

    /// <summary>Resolve as dependências pelo <c>.deps.json</c> do projeto carregado; o resto vem do contexto padrão.</summary>
    private sealed class ProjectLoadContext(string assemblyPath) : AssemblyLoadContext(Path.GetFileNameWithoutExtension(assemblyPath))
    {
        private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
