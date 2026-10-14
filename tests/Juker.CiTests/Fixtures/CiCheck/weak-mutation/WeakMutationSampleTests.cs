using AwesomeAssertions;
using @@NAMESPACE@@;
using Xunit;

namespace @@TEST_NAMESPACE@@;

/// <summary>
/// Teste fraco proposital da branch descartável <c>ci-check/weak-mutation</c> (SPEC-0005:IT-07): executa o código sem
/// verificar o resultado, então quase todo mutante sobrevive. Nunca entra na <c>main</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WeakMutationSampleTests
{
    [Fact]
    [Trait("Spec", "SPEC-0005:IT-07")]
    public void Executa_sem_verificar_o_resultado() =>
        WeakMutationSample.Score(5, 1).Should().BeGreaterThanOrEqualTo(int.MinValue);
}
