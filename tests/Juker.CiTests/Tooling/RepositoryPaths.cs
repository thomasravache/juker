namespace Juker.CiTests.Tooling;

/// <summary>Localiza a raiz do repositório (a pasta que contém <c>Juker.slnx</c>).</summary>
internal static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    public static string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

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
