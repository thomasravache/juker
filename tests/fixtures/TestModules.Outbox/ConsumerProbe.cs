using System.Collections.Concurrent;
using System.Diagnostics;

namespace TestModules.Outbox;

/// <summary>
/// Registra as execuções dos consumidores de teste e decide quando eles falham. Singleton do DI do teste;
/// a espera é por condição (sinal a cada execução), nunca por tempo fixo.
/// </summary>
public sealed class ConsumerProbe(TimeProvider time)
{
    private readonly ConcurrentQueue<Delivery> _deliveries = new();
    private readonly ConcurrentDictionary<string, int> _failuresRemaining = new(StringComparer.Ordinal);
    private readonly Lock _signalLock = new();
    private TaskCompletionSource _changed = NewSignal();

    public IReadOnlyList<Delivery> Deliveries => [.. _deliveries];

    /// <summary>O consumidor <typeparamref name="TConsumer"/> falha nas próximas <paramref name="times"/> execuções.</summary>
    public void FailNext<TConsumer>(int times) => _failuresRemaining[Name<TConsumer>()] = times;

    public IReadOnlyList<Delivery> Of<TConsumer>() => [.. _deliveries.Where(delivery => string.Equals(delivery.Consumer, Name<TConsumer>(), StringComparison.Ordinal))];

    /// <summary>Espera até <paramref name="condition"/> valer para as execuções registradas, ou falha após <paramref name="timeout"/>.</summary>
    public async Task WaitUntilAsync(Func<IReadOnlyList<Delivery>, bool> condition, TimeSpan timeout, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        while (true)
        {
            Task changed;
            lock (_signalLock)
            {
                changed = _changed.Task;
            }

            if (condition(Deliveries))
            {
                return;
            }

            var remaining = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), deadline);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException($"Condição dos consumidores não atingida em {timeout}. Execuções: {Deliveries.Count}.");
            }

            try
            {
                await changed.WaitAsync(remaining, ct).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Reavalia a condição uma última vez no início do laço e então falha pelo prazo.
            }
        }
    }

    /// <summary>Registra a execução e lança exceção se o consumidor deve falhar (configurado ou <paramref name="forceFailure"/>).</summary>
    internal void Record<TConsumer>(Guid eventId, bool forceFailure = false)
    {
        var consumer = Name<TConsumer>();
        var configured = _failuresRemaining.TryGetValue(consumer, out var remaining) && remaining > 0;
        if (configured)
        {
            _failuresRemaining[consumer] = remaining - 1;
        }

        var fail = forceFailure || configured;

        _deliveries.Enqueue(new Delivery(consumer, eventId, !fail, time.GetUtcNow(), Stopwatch.GetTimestamp()));
        Signal();

        if (fail)
        {
            throw new InvalidOperationException($"Falha configurada do consumidor de teste {consumer} para o evento {eventId}.");
        }
    }

    private static string Name<TConsumer>() => typeof(TConsumer).FullName!;

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void Signal()
    {
        TaskCompletionSource previous;
        lock (_signalLock)
        {
            previous = _changed;
            _changed = NewSignal();
        }

        previous.TrySetResult();
    }
}
