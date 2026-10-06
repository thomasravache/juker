using AwesomeAssertions;
using Juker.Infrastructure;
using Juker.SharedKernel;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Juker.BuildingBlocks.UnitTests.Http;

[Trait("Category", "Unit")]
public sealed class ResultHttpExtensionsTests
{
    private const string ProblemJson = "application/problem+json";

    [Theory]
    [Trait("Spec", "SPEC-0004:UT-03")]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.BusinessRule, StatusCodes.Status422UnprocessableEntity)]
    public async Task Falha_vira_ProblemDetails_com_o_status_da_tabela_e_o_code(ErrorType type, int expectedStatus)
    {
        // Dado um Result<T> de falha com o ErrorType
        var error = new Error("queue.limit_reached", "Limite de pedidos da comanda atingido.", type);
        var result = Result<int>.Failure(error);

        // Quando ToHttpResult é executado
        var response = await HttpResultExecutor.ExecuteAsync(result.ToHttpResult(value => Results.Ok(value)));

        // Então o status segue a tabela do contrato e o ProblemDetails traz o code
        response.StatusCode.Should().Be(expectedStatus);
        response.ContentType.Should().StartWith(ProblemJson);
        response.Body.Should().NotBeNull();
        response.Body!.Value.GetProperty("status").GetInt32().Should().Be(expectedStatus);
        response.Body.Value.GetProperty("code").GetString().Should().Be(error.Code);
    }

    [Fact]
    [Trait("Spec", "SPEC-0004:UT-03")]
    public async Task Falha_de_validacao_traz_errors_por_campo()
    {
        // Dado um erro de validação com Details por campo
        var error = new Error("validation", "Dados inválidos.", ErrorType.Validation)
        {
            Details = new Dictionary<string, string[]>(StringComparer.Ordinal) { ["name"] = ["O nome é obrigatório."] },
        };

        // Quando ToHttpResult é executado
        var response = await HttpResultExecutor.ExecuteAsync(Result<int>.Failure(error).ToHttpResult(value => Results.Ok(value)));

        // Então é 400 com code e errors { name: [mensagem] }
        response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        response.ContentType.Should().StartWith(ProblemJson);
        response.Body!.Value.GetProperty("code").GetString().Should().Be("validation");
        var name = response.Body.Value.GetProperty("errors").GetProperty("name");
        name.EnumerateArray().Select(message => message.GetString()).Should().Equal("O nome é obrigatório.");
    }

    [Fact]
    [Trait("Spec", "SPEC-0004:UT-03")]
    public async Task Sucesso_com_valor_usa_o_resultado_de_onSuccess()
    {
        // Quando um Result<int> de sucesso é convertido
        var response = await HttpResultExecutor.ExecuteAsync(Result<int>.Success(7).ToHttpResult(value => Results.Ok(value)));

        // Então a resposta é a produzida por onSuccess
        response.StatusCode.Should().Be(StatusCodes.Status200OK);
        response.Body!.Value.GetInt32().Should().Be(7);
    }

    [Fact]
    [Trait("Spec", "SPEC-0004:UT-03")]
    public async Task Sucesso_sem_valor_e_204()
    {
        // Quando Result.Success() é convertido
        var response = await HttpResultExecutor.ExecuteAsync(Result.Success().ToHttpResult());

        // Então é 204 sem corpo
        response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        response.Body.Should().BeNull();
    }

    [Fact]
    [Trait("Spec", "SPEC-0004:UT-03")]
    public async Task Falha_sem_valor_tambem_vira_ProblemDetails()
    {
        // Dado um Result (sem valor) de falha
        var error = new Error("venue.not_found", "Bar não encontrado.", ErrorType.NotFound);

        // Quando é convertido
        var response = await HttpResultExecutor.ExecuteAsync(Result.Failure(error).ToHttpResult());

        // Então é 404 com o code
        response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        response.ContentType.Should().StartWith(ProblemJson);
        response.Body!.Value.GetProperty("code").GetString().Should().Be("venue.not_found");
    }
}
