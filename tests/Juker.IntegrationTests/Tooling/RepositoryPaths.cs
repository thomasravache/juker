namespace Juker.IntegrationTests.Tooling;

/// <summary>Localiza a raiz do repositório (a pasta que contém <c>Juker.slnx</c>).</summary>
internal static class RepositoryPaths
{
    private static readonly string[] BuildOutputDirectories = ["bin", "obj", "artifacts"];

    public static string Root { get; } = FindRoot();

    public static string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>Arquivos de código-fonte sob <paramref name="topLevelDirectory"/>, ignorando saídas de build.</summary>
    public static IEnumerable<string> SourceFiles(string topLevelDirectory, string searchPattern)
    {
        var directory = Combine(topLevelDirectory);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file));
    }

    private static bool IsBuildOutput(string file)
    {
        var relative = Path.GetRelativePath(Root, file);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => BuildOutputDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Juker.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Juker.slnx não encontrado acima de '{AppContext.BaseDirectory}'.");
    }
}
