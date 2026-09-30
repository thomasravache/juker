namespace Juker.Modules.Queue.Domain;

/// <summary>Tipo interno do módulo Queue: alvo das violações ARQ-01 e ARQ-05.</summary>
public sealed class QueueItem
{
    public Guid Id { get; init; }
}
