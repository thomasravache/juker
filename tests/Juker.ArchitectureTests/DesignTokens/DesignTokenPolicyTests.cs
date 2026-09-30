using AwesomeAssertions;
using Xunit;

namespace Juker.ArchitectureTests.DesignTokens;

[Trait("Category", "Unit")]
public sealed class DesignTokenPolicyTests
{
    // Linhas do Fixtures/DesignTokens/Bad.razor com #ff007a, bg-[#fff] e font-family
    private static readonly int[] BadLines = [3, 4, 5];

    [Fact]
    [Trait("Spec", "SPEC-0003:UT-02")]
    public void Politica_aponta_as_tres_violacoes_do_Bad_razor_e_ignora_o_arquivo_de_tokens()
    {
        // Dado os fixtures Bad.razor (cor literal, valor arbitrário e font-family) e theme.css (com cores)
        var fixtures = RepositoryPaths.Combine("tests", "Juker.ArchitectureTests", "Fixtures", "DesignTokens");
        var badRazor = Path.Combine(fixtures, "Bad.razor");
        var theme = Path.Combine(fixtures, "theme.css");
        File.Exists(badRazor).Should().BeTrue();
        File.Exists(theme).Should().BeTrue();

        // Quando a política varre os fixtures, com o theme.css como arquivo de tokens
        var violations = DesignTokenPolicy.Scan(fixtures, theme);

        // Então aponta as três violações do Bad.razor com arquivo e linha, e nada no theme.css
        violations.Should().OnlyContain(violation => Path.GetFullPath(violation.File) == Path.GetFullPath(badRazor));
        violations.Select(violation => violation.Line).Distinct().Should().BeEquivalentTo(BadLines);
        violations.Should().Contain(violation => violation.Match.Contains("#ff007a", StringComparison.OrdinalIgnoreCase));
        violations.Should().Contain(violation => violation.Match.Contains("#fff", StringComparison.OrdinalIgnoreCase));
        violations.Should().Contain(violation => violation.Match.Contains("font-family", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Spec", "SPEC-0003:UT-02")]
    public void Arquivos_de_UI_reais_da_solucao_respeitam_a_politica()
    {
        // Dado os arquivos de UI da solução, com src/Web/Juker.Web/Styles/theme.css como arquivo de tokens
        var source = RepositoryPaths.Combine("src");
        var tokens = RepositoryPaths.Combine("src", "Web", "Juker.Web", "Styles", "theme.css");

        // Quando a política varre, então não há violações
        var violations = DesignTokenPolicy.Scan(source, tokens);
        violations.Should().BeEmpty();
    }
}
