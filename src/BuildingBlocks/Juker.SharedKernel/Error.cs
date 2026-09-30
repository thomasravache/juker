using System.Diagnostics.CodeAnalysis;

namespace Juker.SharedKernel;

/// <summary>Erro de negócio com código estável (vira o <c>code</c> do ProblemDetails).</summary>
/// <param name="Code">Código estável do erro (por exemplo <c>queue.limit_reached</c>).</param>
/// <param name="Message">Mensagem legível.</param>
/// <param name="Type">Categoria do erro.</param>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Nome do contrato (SPEC-0004, seção 6).")]
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Erros por campo (validação).</summary>
    public IReadOnlyDictionary<string, string[]>? Details { get; init; }
}
