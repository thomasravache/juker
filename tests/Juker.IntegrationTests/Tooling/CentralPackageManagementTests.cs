using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace Juker.IntegrationTests.Tooling;

[Trait("Category", "Unit")]
public sealed class CentralPackageManagementTests
{
    [Fact]
    [Trait("Spec", "SPEC-0002:UT-02")]
    public void Projetos_nao_declaram_versao_e_o_gerenciamento_central_e_estrito()
    {
        // Dado qualquer .csproj sob src/ e tests/
        var projects = RepositoryPaths.SourceFiles("src", "*.csproj")
            .Concat(RepositoryPaths.SourceFiles("tests", "*.csproj"))
            .ToList();
        projects.Should().NotBeEmpty();

        // Quando lido como XML, então nenhum PackageReference declara Version
        var versionedReferences = projects
            .SelectMany(project => XDocument.Load(project)
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal))
                .Where(element => element.Attribute("Version") is not null)
                .Select(element => $"{Path.GetRelativePath(RepositoryPaths.Root, project)}: {element.Attribute("Include")?.Value}"))
            .ToList();
        versionedReferences.Should().BeEmpty("as versões ficam só no Directory.Packages.props");

        // E o Directory.Packages.props define o gerenciamento central estrito
        var packagesProps = RepositoryPaths.Combine("Directory.Packages.props");
        File.Exists(packagesProps).Should().BeTrue("Directory.Packages.props deve existir na raiz");
        var properties = XDocument.Load(packagesProps).Descendants().ToList();

        PropertyValue(properties, "ManagePackageVersionsCentrally").Should().BeEquivalentTo("true");
        PropertyValue(properties, "CentralPackageVersionOverrideEnabled").Should().BeEquivalentTo("false");
    }

    private static string? PropertyValue(IEnumerable<XElement> elements, string name) =>
        elements
            .LastOrDefault(element => string.Equals(element.Name.LocalName, name, StringComparison.Ordinal))
            ?.Value
            .Trim();
}
