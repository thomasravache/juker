using Xunit;

namespace Juker.ArchitectureTests.ConventionViolations;

/// <summary>Dentro da convenção: Category na classe e Spec no método (como os testes da SPEC-0002).</summary>
[Trait("Category", "Unit")]
public sealed class CompliantTests
{
    [Fact(Timeout = 1_000)]
    [Trait("Spec", "SPEC-0099:UT-01")]
    public void Fact_com_spec_no_metodo_e_categoria_na_classe()
    {
    }

    [Theory]
    [InlineData(1)]
    [Trait("Spec", "SPEC-0099:E2E-02")]
    [Trait("Spec", "SPEC-0098:CH-03")]
    public void Theory_com_duas_specs(int value) => _ = value;

    [Fact]
    [Trait("Spec", "SPEC-0099:CT-04")]
    [Trait("Category", "Smoke")]
    public void Fact_com_categoria_tambem_no_metodo()
    {
    }

    public static void Metodo_auxiliar_sem_fact()
    {
    }
}
