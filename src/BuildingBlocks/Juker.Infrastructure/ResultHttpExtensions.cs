using Juker.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace Juker.Infrastructure;

/// <summary>Converte <see cref="Result"/> em resposta HTTP: sucesso ou ProblemDetails (RFC 9457) com <c>code</c>.</summary>
public static class ResultHttpExtensions
{
    /// <summary>Sucesso vira 204; falha vira ProblemDetails.</summary>
    public static IResult ToHttpResult(this Result r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return r.IsSuccess ? Results.NoContent() : CreateProblemResult(r.Error!);
    }

    /// <summary>Sucesso vira <paramref name="onSuccess"/>; falha vira ProblemDetails.</summary>
    public static IResult ToHttpResult<T>(this Result<T> r, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return r.IsSuccess ? onSuccess(r.Value) : CreateProblemResult(r.Error!);
    }

    private static IResult CreateProblemResult(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError,
        };

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = error.Code,
        };

        if (error.Type == ErrorType.Validation && error.Details is not null)
        {
            extensions["errors"] = error.Details;
        }

        return Results.Problem(
            statusCode: statusCode,
            title: error.Message,
            extensions: extensions);
    }
}
