using AwesomeAssertions;
using Juker.CiTests.Tooling;
using Xunit;

namespace Juker.CiTests.Workflows;

/// <summary>
/// SPEC-0005:CT-01 — o <c>.github/dependabot.yml</c> (ADR-0017, emenda v3): NuGet e GitHub Actions toda semana, com as
/// atualizações agrupadas num PR por ecossistema e no máximo 2 PRs abertos por ecossistema.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DependabotContractTests
{
    private const string OpenPullRequestsLimit = "2";

    public static readonly TheoryData<string> Ecosystems = ["nuget", "github-actions"];

    [Theory]
    [MemberData(nameof(Ecosystems))]
    [Trait("Spec", "SPEC-0005:CT-01")]
    public void Dependabot_atualiza_o_ecossistema_toda_semana_agrupado_e_com_no_maximo_2_PRs(string ecosystem)
    {
        // Dado o .github/dependabot.yml
        var path = RepositoryPaths.Combine(".github", "dependabot.yml");
        File.Exists(path).Should().BeTrue("o .github/dependabot.yml existe (SPEC-0005, ADR-0017)");
        var dependabot = YamlNodes.LoadMapping(path);
        dependabot.Text("version").Should().Be("2");

        // Quando a entrada do ecossistema é lida
        var updates = dependabot.Child("updates").Items()
            .Where(update => string.Equals(update.Text("package-ecosystem"), ecosystem, StringComparison.Ordinal))
            .ToList();
        updates.Should().ContainSingle("o Dependabot tem uma entrada para '{0}'", ecosystem);
        var update = updates[0];

        // Então ela é semanal, com no máximo 2 PRs abertos
        update.Child("schedule").Text("interval").Should().Be("weekly");
        update.Text("open-pull-requests-limit").Should().Be(OpenPullRequestsLimit);

        // E agrupa todas as dependências do ecossistema num PR (um grupo com o padrão "*")
        update.Child("groups").Entries()
            .Should().Contain(group => group.Value.Child("patterns").Texts().Contains("*"),
                "as atualizações de '{0}' chegam agrupadas num PR (emenda v3)", ecosystem);
    }
}
