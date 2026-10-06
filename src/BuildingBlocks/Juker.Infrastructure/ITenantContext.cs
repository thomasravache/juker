using Juker.SharedKernel;

namespace Juker.Infrastructure;

/// <summary>O bar do contexto atual (ADR-0008); <c>null</c> quando não resolvido (o filtro de tenant falha fechado).</summary>
public interface ITenantContext
{
    VenueId? VenueId { get; }
}
