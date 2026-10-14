using System.Diagnostics;
using Microsoft.Playwright;

namespace Juker.E2ETests.Harness;

/// <summary>
/// Espera por condição (ADR-0013, regra 9): consulta a URL a cada tique de um <see cref="PeriodicTimer"/> até o status
/// esperado ou o prazo, sem <c>Thread.Sleep</c>/<c>Task.Delay</c>.
/// </summary>
internal static class HttpPolling
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);
    private const float RequestTimeoutMilliseconds = 5_000;

    /// <summary>O último status observado: o esperado, se chegou dentro do prazo.</summary>
    public static async Task<int?> WaitForStatusAsync(
        IAPIRequestContext request,
        string url,
        int expected,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var start = Stopwatch.GetTimestamp();
        using var timer = new PeriodicTimer(Interval);
        int? last = null;
        do
        {
            last = await TryGetStatusAsync(request, url) ?? last;
            if (last == expected || Stopwatch.GetElapsedTime(start) > timeout)
            {
                return last;
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));

        return last;
    }

    private static async Task<int?> TryGetStatusAsync(IAPIRequestContext request, string url)
    {
        try
        {
            var response = await request.GetAsync(url, new() { Timeout = RequestTimeoutMilliseconds, FailOnStatusCode = false });
            await using (response)
            {
                return response.Status;
            }
        }
        catch (PlaywrightException)
        {
            // host ainda não aceita conexões: tenta de novo no próximo tique
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
    }
}
