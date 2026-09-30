using Xunit;

namespace Juker.ArchitectureTests.ConventionViolations;

/// <summary>Fora da convenção: cada método tem um defeito diferente de traits.</summary>
public sealed class NonCompliantTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Sem_spec()
    {
    }

    [Fact]
    [Trait("Spec", "SPEC-0099:IT-01")]
    public void Sem_categoria()
    {
    }

    [Fact]
    [Trait("Spec", "SPEC-99:UT-1")]
    [Trait("Category", "Unit")]
    public void Spec_fora_do_formato()
    {
    }

    [Fact]
    [Trait("Spec", "SPEC-0099:XX-01")]
    [Trait("Category", "Integration")]
    public void Spec_com_nivel_desconhecido()
    {
    }

    [Fact]
    [Trait("Spec", "SPEC-0099:UT-02")]
    [Trait("Category", "Slow")]
    public void Categoria_nao_permitida()
    {
    }

    [Theory]
    [InlineData(1)]
    public void Theory_sem_traits(int value) => _ = value;
}
