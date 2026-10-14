using AwesomeAssertions;
using Xunit;

namespace Juker.BuildingBlocks.UnitTests.CiCheck;

/// <summary>
/// Teste vermelho proposital da branch descartável <c>ci-check/red-test</c> (SPEC-0005:IT-01): prova que o job
/// <c>unit</c> falha e bloqueia o PR. Nunca entra na <c>main</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TesteVermelhoPropositalTests
{
    [Fact]
    [Trait("Spec", "SPEC-0005:IT-01")]
    public void Falha_de_proposito_para_verificar_o_job_unit() =>
        (1 + 1).Should().Be(3, "teste vermelho proposital da verificação SPEC-0005:IT-01");
}
