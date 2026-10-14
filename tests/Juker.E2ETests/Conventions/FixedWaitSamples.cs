namespace Juker.E2ETests.Conventions;

/// <summary>
/// Amostras do UT-01 (SPEC-0025): métodos que nunca são executados, só lidos pelo <see cref="FixedWaitScanner"/>.
/// Não são testes, então a convenção sobre o assembly real não os avalia.
/// </summary>
internal static class FixedWaitSamples
{
    public static void DormeComThreadSleep()
    {
        // Amostra proposital do UT-01: espera fixa que o scanner deve apontar (nunca executada)
#pragma warning disable RS0030 // a chamada proibida é o objeto do teste
        Thread.Sleep(TimeSpan.FromMilliseconds(1));
#pragma warning restore RS0030
    }

    public static async Task EsperaComTaskDelayNoCorpoAsync(CancellationToken cancellationToken)
    {
        // Amostra proposital do UT-01: a chamada fica na máquina de estados do método async (nunca executada)
#pragma warning disable RS0030 // a chamada proibida é o objeto do teste
        await Task.Delay(1, cancellationToken);
#pragma warning restore RS0030
    }

    public static Func<Task> EsperaComTaskDelayNumaLambda()
    {
        // Amostra proposital do UT-01: a chamada fica no método gerado para a lambda (nunca executada)
#pragma warning disable RS0030 // a chamada proibida é o objeto do teste
        return () => Task.Delay(TimeSpan.FromMilliseconds(1));
#pragma warning restore RS0030
    }

    public static async Task EsperaPorCondicaoAsync(CancellationToken cancellationToken)
    {
        // Espera por condição: permitida
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1));
        await timer.WaitForNextTickAsync(cancellationToken);
    }

    public static int SemEspera(int value) => value + 1;
}
