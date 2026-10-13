namespace Juker.SharedKernel;

/// <summary>Categoria de um <see cref="Error"/>; define o status HTTP do ProblemDetails (SPEC-0004, seção 6).</summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    BusinessRule,
    Unauthorized,
}
