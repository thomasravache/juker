using System.Reflection;
using System.Runtime.Loader;
using Juker.IntegrationTests.Tooling;

namespace Juker.IntegrationTests.Testing;

/// <summary>
/// A saída compilada do <c>tests/Juker.E2ETests</c> (mesma configuração deste build), onde o <c>Juker.TestKit.E2E</c>
/// fica com todas as dependências (Playwright, xUnit). Este projeto não referencia os projetos de E2E: o
/// SPEC-0025:CT-01 lê o assembly dessa saída num contexto de carga isolado, como o <c>arch_test</c> faz com os
/// projetos de teste. A saída existe depois do build da solução (<c>dotnet test --solution Juker.slnx</c>).
/// </summary>
internal static class E2ETestsOutput
{
    private const string TestKitE2E = "Juker.TestKit.E2E";

    /// <summary>Carrega o <c>Juker.TestKit.E2E</c>, chama <paramref name="read"/> e descarrega o contexto.</summary>
    public static T ReadTestKitE2E<T>(Func<Assembly, T> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        var thisProject = RepositoryPaths.Combine("tests", "Juker.IntegrationTests");
        var outputLayout = Path.GetRelativePath(thisProject, AppContext.BaseDirectory);
        var output = Path.GetFullPath(Path.Combine(RepositoryPaths.Combine("tests", "Juker.E2ETests"), outputLayout));
        var entry = Path.Combine(output, "Juker.E2ETests.dll");
        if (!File.Exists(entry))
        {
            throw new InvalidOperationException($"Saída do Juker.E2ETests não encontrada em '{output}'. Compile a solução antes (dotnet build Juker.slnx).");
        }

        var context = new OutputLoadContext(entry);
        try
        {
            return read(context.LoadFromAssemblyPath(Path.Combine(output, TestKitE2E + ".dll")));
        }
        finally
        {
            context.Unload();
        }
    }

    private sealed class OutputLoadContext(string entryAssemblyPath) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(entryAssemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
