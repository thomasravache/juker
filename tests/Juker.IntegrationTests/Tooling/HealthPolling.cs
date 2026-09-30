using System.Net;

namespace Juker.IntegrationTests.Tooling;

/// <summary>Espera por condição (sem Thread.Sleep/Task.Delay): consulta o endpoint até o status esperado ou o prazo.</summary>
internal static class HealthPolling
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public static async Task<HttpStatusCode?> WaitForStatusAsync(
        HttpClient client,
        string path,
        HttpStatusCode expected,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var timer = new PeriodicTimer(Interval);

        HttpStatusCode? last = null;
        try
        {
            do
            {
                last = await TryGetStatusAsync(client, path, deadline.Token) ?? last;
                if (last == expected)
                {
                    return last;
                }
            }
            while (await timer.WaitForNextTickAsync(deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // prazo esgotado: devolve o último status observado para a asserção do teste
        }

        return last;
    }

    private static async Task<HttpStatusCode?> TryGetStatusAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        request.CancelAfter(RequestTimeout);
        try
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), request.Token);
            return response.StatusCode;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
