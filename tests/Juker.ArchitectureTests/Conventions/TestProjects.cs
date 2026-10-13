using System.Xml.Linq;

namespace Juker.ArchitectureTests.Conventions;

/// <summary>
/// Os projetos de <c>tests/**</c> de uma solução (<c>.slnx</c>), sem fixtures, e as convenções sobre eles
/// (SPEC-0027): todo projeto é <c>ProjectReference</c> do <c>Juker.ArchitectureTests.csproj</c> (UT-02) e o opt-in
/// <c>JukerAllowZeroTests</c> bate com a contagem de testes de cada projeto de teste (UT-03).
/// </summary>
/// <remarks>Os caminhos são relativos à pasta da solução, com <c>/</c> como separador (como no <c>.slnx</c>).</remarks>
public static class TestProjects
{
    private const string TestsDirectory = "tests/";
    private const string AllowZeroTestsProperty = "JukerAllowZeroTests";
    private const string IsTestProjectProperty = "IsTestProject";

    // Fixtures dos próprios testes: tests/fixtures/** e as pastas Fixtures/ dentro dos projetos de teste.
    private const string FixturesSegment = "fixtures";

    /// <summary>Os projetos de <c>tests/**</c> listados na solução, exceto fixtures.</summary>
    public static IReadOnlyList<string> InSolution(string solutionFile)
    {
        ArgumentNullException.ThrowIfNull(solutionFile);

        return XDocument.Load(solutionFile).Descendants("Project")
            .Select(project => (string?)project.Attribute("Path"))
            .OfType<string>()
            .Select(Normalize)
            .Where(path => path.StartsWith(TestsDirectory, StringComparison.Ordinal))
            .Where(path => !path.Split('/').Contains(FixturesSegment, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Os projetos de <c>tests/**</c> da solução que o projeto de arquitetura não referencia (<c>ProjectReference</c>),
    /// relativos à pasta da solução.
    /// </summary>
    public static IReadOnlyList<string> MissingReferences(string solutionFile, string architectureProjectFile)
    {
        ArgumentNullException.ThrowIfNull(solutionFile);
        ArgumentNullException.ThrowIfNull(architectureProjectFile);

        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionFile))!;
        var architectureDirectory = Path.GetDirectoryName(Path.GetFullPath(architectureProjectFile))!;
        var architectureProject = Normalize(Path.GetRelativePath(solutionDirectory, Path.GetFullPath(architectureProjectFile)));

        var referenced = XDocument.Load(architectureProjectFile).Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .OfType<string>()
            .Select(include => Path.GetFullPath(Path.Combine(architectureDirectory, include.Replace('\\', Path.DirectorySeparatorChar))))
            .Select(path => Normalize(Path.GetRelativePath(solutionDirectory, path)))
            .ToHashSet(StringComparer.Ordinal);

        return InSolution(solutionFile)
            .Where(project => !string.Equals(project, architectureProject, StringComparison.Ordinal))
            .Where(project => !referenced.Contains(project))
            .ToList();
    }

    /// <summary>Se o <c>.csproj</c> declara <c>IsTestProject=true</c> (bibliotecas de apoio, como o TestKit, não declaram).</summary>
    public static bool IsTestProject(string projectFile) => BooleanProperty(projectFile, IsTestProjectProperty);

    /// <summary>Se o <c>.csproj</c> opta por aceitar zero testes (<c>JukerAllowZeroTests=true</c>).</summary>
    public static bool AllowsZeroTests(string projectFile) => BooleanProperty(projectFile, AllowZeroTestsProperty);

    /// <summary>
    /// Os projetos de teste fora da regra de zero testes: sem o opt-in e sem nenhum teste, ou com o opt-in e com testes.
    /// </summary>
    public static IReadOnlyList<ZeroTestsViolation> FindZeroTestsViolations(IEnumerable<TestProjectCount> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        return projects
            .Select(project => (project.AllowsZeroTests, project.TestCount) switch
            {
                (false, 0) => new ZeroTestsViolation(
                    project.Project,
                    $"sem {AllowZeroTestsProperty} e sem nenhum [Fact]/[Theory]: o projeto perdeu os testes"),
                (true, > 0) => new ZeroTestsViolation(
                    project.Project,
                    $"tem {project.TestCount} teste(s) e ainda declara {AllowZeroTestsProperty}=true: remova o opt-in"),
                _ => null,
            })
            .OfType<ZeroTestsViolation>()
            .ToList();
    }

    private static bool BooleanProperty(string projectFile, string property)
    {
        ArgumentNullException.ThrowIfNull(projectFile);

        return XDocument.Load(projectFile).Descendants(property)
            .Any(element => string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
