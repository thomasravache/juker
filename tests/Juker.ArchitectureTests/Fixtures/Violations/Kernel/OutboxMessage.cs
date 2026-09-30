namespace Juker.Infrastructure;

/// <summary>Simula um tipo do Juker.Infrastructure: proibido num .Contracts (ARQ-02, regra estrita).</summary>
public sealed record OutboxMessage(Guid Id, string Type);
