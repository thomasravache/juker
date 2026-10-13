namespace Juker.Infrastructure.Outbox;

internal sealed class OutboxDrainer(OutboxProcessor processor) : IOutboxDrainer
{
    public Task<int> DrainAsync(CancellationToken ct) =>
        processor.ProcessAllAsync(drainAll: true, ct);
}
