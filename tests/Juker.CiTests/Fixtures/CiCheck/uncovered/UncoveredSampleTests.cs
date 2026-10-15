using AwesomeAssertions;
using Juker.Modules.Queue.CiCheck;
using Xunit;

namespace Juker.Modules.Queue.UnitTests.CiCheck;

/// <summary>
/// Segunda fase da branch descartável <c>ci-check/uncovered</c> (SPEC-0005:IT-06): com o método coberto, o job
/// <c>coverage</c> passa. Nunca entra na <c>main</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class UncoveredSampleTests
{
    [Theory]
    [InlineData(0, "vazia")]
    [InlineData(3, "curta")]
    [InlineData(42, "longa")]
    [Trait("Spec", "SPEC-0005:IT-06")]
    public void Classifica_o_tamanho(int size, string expected) =>
        UncoveredSample.Classify(size).Should().Be(expected);

    [Fact]
    [Trait("Spec", "SPEC-0005:IT-06")]
    public void Tamanho_negativo_e_rejeitado()
    {
        var classify = () => UncoveredSample.Classify(-1);

        classify.Should().Throw<ArgumentOutOfRangeException>();
    }
}
