using AwesomeAssertions;
using Xunit;

namespace Juker.ArchitectureTests.DesignTokens;

[Trait("Category", "Unit")]
public sealed class DesignTokenPolicyTests
{
    // Violações esperadas nos fixtures (arquivo, linha). Bad.razor linha 11 (href="#add") é âncora, não cor.
    private static readonly (string File, int Line)[] ExpectedViolations =
    [
        ("Bad.razor", 3),      // style="color: #ff007a"
        ("Bad.razor", 4),      // bg-[#fff]
        ("Bad.razor", 5),      // font-family
        ("Bad.razor", 6),      // fill="#ff007a" (atributo entre aspas duplas)
        ("Bad.razor", 7),      // stroke='#fff' (atributo entre aspas simples)
        ("Bad.razor", 8),      // parâmetro de componente Color="#ff007a"
        ("Bad.razor", 9),      // rgb(
        ("Bad.razor", 10),     // hsl(
        ("Bad.css", 3),        // color: #ff007a
        ("Bad.css", 4),        // font-family
        ("Bad.razor.css", 3),  // rgb(
        ("Bad.razor.css", 4),  // font-family
    ];

    [Fact]
    [Trait("Spec", "SPEC-0003:UT-02")]
    public void Politica_aponta_exatamente_as_violacoes_dos_fixtures_e_ignora_o_arquivo_de_tokens()
    {
        // Dado os fixtures Bad.razor, Bad.css e Bad.razor.css (cores literais, valor arbitrário, font-family,
        // atributos entre aspas e parâmetro de componente), uma âncora href="#add" e o theme.css (com cores)
        var fixtures = RepositoryPaths.Combine("tests", "Juker.ArchitectureTests", "Fixtures", "DesignTokens");
        var theme = Path.Combine(fixtures, "theme.css");
        foreach (var file in ExpectedViolations.Select(expected => expected.File).Distinct(StringComparer.Ordinal).Append("theme.css"))
        {
            File.Exists(Path.Combine(fixtures, file)).Should().BeTrue($"o fixture {file} deve existir");
        }

        // Quando a política varre os fixtures, com o theme.css como arquivo de tokens
        var violations = DesignTokenPolicy.Scan(fixtures, theme);

        // Então aponta exatamente essas linhas (nem o href="#add" nem o theme.css)
        violations
            .Select(violation => (File: Path.GetRelativePath(fixtures, violation.File), violation.Line))
            .Distinct()
            .Should().BeEquivalentTo(ExpectedViolations);
        violations.Should().OnlyContain(violation => !string.IsNullOrWhiteSpace(violation.Match));
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
