namespace Juker.Infrastructure.Outbox;

internal sealed class InboxMessage
{
    public Guid MessageId { get; set; }

    public string Consumer { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; set; }
}
