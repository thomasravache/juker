namespace Juker.SharedKernel;

/// <summary>Resultado de uma operação: sucesso ou falha com <see cref="Error"/> (sem exceções para fluxo de negócio).</summary>
public class Result
{
    protected Result(bool isSuccess, Error? error) => throw new NotImplementedException();

    public bool IsSuccess => throw new NotImplementedException();

    public bool IsFailure => throw new NotImplementedException();

    public Error? Error => throw new NotImplementedException();

    public static Result Success() => throw new NotImplementedException();

    public static Result Failure(Error error) => throw new NotImplementedException();
}
