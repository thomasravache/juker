namespace @@NAMESPACE@@;

/// <summary>
/// Classe de exemplo da branch descartável <c>ci-check/weak-mutation</c> (SPEC-0005:IT-07): o teste dela é fraco, os
/// mutantes sobrevivem e o <c>mutation.yml</c> termina vermelho por score abaixo de 80%. Nunca entra na <c>main</c>.
/// </summary>
public static class WeakMutationSample
{
    /// <summary>Uma conta qualquer com desvios e operadores para o Stryker mutar (sem significado no produto).</summary>
    public static int Score(int requests, int skips)
    {
        if (requests <= 0)
        {
            return 0;
        }

        var score = (requests * 10) - (skips * 3);
        return score > 100 ? 100 : score;
    }
}
