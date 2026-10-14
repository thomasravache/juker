namespace Juker.Host.Testing;

/// <summary>
/// Relógio do servidor no ambiente E2E (SPEC-0025, ADR-0013 regra 3): segue o relógio do sistema e soma um
/// deslocamento que o teste avança por <c>POST /_test/clock/advance</c>. Os timers e o <c>GetTimestamp</c> continuam
/// os do sistema: avançar o relógio muda o "agora" (expirações, viradas de noite), não dispara timers.
/// </summary>
internal sealed class ControllableTimeProvider(TimeProvider inner) : TimeProvider
{
    private long _offsetTicks;

    /// <summary>Quanto o relógio está à frente do relógio do sistema.</summary>
    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow() + Offset;

    /// <summary>Avança o relógio (nunca para trás).</summary>
    public void Advance(TimeSpan by)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);
        Interlocked.Add(ref _offsetTicks, by.Ticks);
    }
}
