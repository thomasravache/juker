namespace Juker.Modules.Sessions.Domain;

/// <summary>Tipo interno do módulo Sessions: alvo da violação ARQ-02.</summary>
public sealed class Session
{
    public Guid Id { get; init; }
}
