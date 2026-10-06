using System.Diagnostics.CodeAnalysis;

namespace Juker.SharedKernel;

/// <summary>Resultado com valor; <see cref="Value"/> de uma falha lança <see cref="InvalidOperationException"/>.</summary>
/// <typeparam name="T">Tipo do valor em caso de sucesso.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, Error? error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não é possível acessar o valor de um Result que falhou.");

    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Fábrica do contrato (SPEC-0004, seção 6).")]
    public static Result<T> Success(T value) => new(true, value, null);

    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Fábrica do contrato (SPEC-0004, seção 6).")]
    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, default, error);
    }
}
