namespace Juker.Infrastructure;

/// <summary>Opções do outbox (seção de configuração <c>Outbox</c>).</summary>
public sealed class OutboxOptions
{
    public bool DispatcherEnabled { get; set; } = true;

    public int IntervalMs { get; set; } = 500;

    public int MaxAttempts { get; set; } = 5;
}
