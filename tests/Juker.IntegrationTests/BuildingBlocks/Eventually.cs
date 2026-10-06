using System.Diagnostics;

namespace Juker.IntegrationTests.BuildingBlocks;

/// <summary>
/// Espera por condição com prazo (ADR-0012): reavalia a cada tique de um <see cref="PeriodicTimer"/> real, sem
/// <c>Thread.Sleep</c>/<c>Task.Delay</c>. O <paramref name="onTick"/> opcional avança o <c>FakeTimeProvider</c>
/// quando o dispatcher precisa de tempo para seguir.
/// </summary>
internal static class Eventually
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(50);

    public static async Task UntilAsync(
        string description,
        Func<Task<bool>> condition,
        TimeSpan timeout,
        CancellationToken ct,
        Action? onTick = null,
        TimeSpan? tickEvery = null)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var start = Stopwatch.GetTimestamp();
        var lastTick = start;
        using var timer = new PeriodicTimer(Poll);
        while (!await condition())
        {
            if (Stopwatch.GetElapsedTime(start) > timeout)
            {
                throw new TimeoutException($"Condição não atingida em {timeout}: {description}.");
            }

            await timer.WaitForNextTickAsync(ct);
            if (onTick is not null && Stopwatch.GetElapsedTime(lastTick) >= (tickEvery ?? Poll))
            {
                onTick();
                lastTick = Stopwatch.GetTimestamp();
            }
        }
    }
}
