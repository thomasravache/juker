namespace Juker.IntegrationTests.BuildingBlocks;

/// <summary>Uma linha de <c>&lt;schema&gt;.inbox_messages</c> (SPEC-0004, seção 6).</summary>
internal sealed record InboxRow(Guid MessageId, string Consumer, DateTimeOffset ProcessedAt);
