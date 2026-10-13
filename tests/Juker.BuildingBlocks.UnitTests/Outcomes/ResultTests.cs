using AwesomeAssertions;
using Juker.SharedKernel;
using Xunit;

namespace Juker.BuildingBlocks.UnitTests.Outcomes;

[Trait("Category", "Unit")]
public sealed class ResultTests
{
    private static readonly Error NotFound = new("venue.not_found", "Bar não encontrado.", ErrorType.NotFound);

    [Fact]
    [Trait("Spec", "SPEC-0004:UT-01")]
    public void Value_de_uma_falha_lanca_InvalidOperationException()
    {
        // Dado um Result<int> de falha
        var result = Result<int>.Failure(NotFound);

        // Então os estados são coerentes e o erro é o informado
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(NotFound);

        // Quando Value é acessado, então lança InvalidOperationException
        var read = () => result.Value;
        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("Spec", "SPEC-0004:UT-01")]
    public void Success_com_valor_e_sucesso_e_expoe_o_valor()
    {
        // Quando Result<int>.Success(1) é criado
        var result = Result<int>.Success(1);

        // Então IsSuccess é verdadeiro, IsFailure é falso, sem erro e com o valor
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
        result.Value.Should().Be(1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0004:UT-01")]
    public void Result_sem_valor_expoe_o_erro_da_falha()
    {
        // Quando Result.Failure(e) e Result.Success() são criados
        var failure = Result.Failure(NotFound);
        var success = Result.Success();

        // Então a falha carrega o erro informado e o sucesso não tem erro
        failure.IsFailure.Should().BeTrue();
        failure.IsSuccess.Should().BeFalse();
        failure.Error.Should().BeSameAs(NotFound);
        success.IsSuccess.Should().BeTrue();
        success.IsFailure.Should().BeFalse();
        success.Error.Should().BeNull();
    }
}
