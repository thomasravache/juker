using System.Diagnostics.CodeAnalysis;

namespace Juker.SharedKernel;

/// <summary>Resultado com valor; <see cref="Value"/> de uma falha lança <see cref="InvalidOperationException"/>.</summary>
/// <typeparam name="T">Tipo do valor em caso de sucesso.</typeparam>
public sealed class Result<T> : Result
{
    private Result(bool isSuccess, Error? error)
        : base(isSuccess, error)
    {
    }

    public T Value => throw new NotImplementedException();

    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Fábrica do contrato (SPEC-0004, seção 6).")]
    public static Result<T> Success(T value) => throw new NotImplementedException();

    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Fábrica do contrato (SPEC-0004, seção 6).")]
    public static new Result<T> Failure(Error error) => throw new NotImplementedException();
}
