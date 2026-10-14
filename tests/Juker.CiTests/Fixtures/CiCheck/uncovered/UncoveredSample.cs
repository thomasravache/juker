namespace Juker.Modules.Queue.CiCheck;

/// <summary>
/// Código novo sem teste da branch descartável <c>ci-check/uncovered</c> (SPEC-0005:IT-06): derruba a cobertura das
/// linhas alteradas abaixo de 80% para que o job <c>coverage</c> falhe. Nunca entra na <c>main</c>.
/// </summary>
public static class UncoveredSample
{
    /// <summary>Classifica um tamanho qualquer (sem significado no produto).</summary>
    public static string Classify(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        if (size == 0)
        {
            return "vazia";
        }

        if (size < 10)
        {
            return "curta";
        }

        return "longa";
    }
}
