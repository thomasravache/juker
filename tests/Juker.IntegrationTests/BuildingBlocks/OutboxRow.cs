namespace Juker.IntegrationTests.BuildingBlocks;

/// <summary>Uma linha de <c>&lt;schema&gt;.outbox_messages</c>, lida por SQL com os nomes do contrato (SPEC-0004, seção 6).</summary>
internal sealed record OutboxRow(
    Guid Id,
    string Type,
    string Payload,
    DateTimeOffset OccurredAt,
    string Status,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? ProcessedAt,
    string? LastError);
