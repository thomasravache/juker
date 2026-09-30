using Juker.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace Juker.Infrastructure;

/// <summary>Converte <see cref="Result"/> em resposta HTTP: sucesso ou ProblemDetails (RFC 9457) com <c>code</c>.</summary>
public static class ResultHttpExtensions
{
    /// <summary>Sucesso vira 204; falha vira ProblemDetails.</summary>
    public static IResult ToHttpResult(this Result r) => throw new NotImplementedException();

    /// <summary>Sucesso vira <paramref name="onSuccess"/>; falha vira ProblemDetails.</summary>
    public static IResult ToHttpResult<T>(this Result<T> r, Func<T, IResult> onSuccess) => throw new NotImplementedException();
}
