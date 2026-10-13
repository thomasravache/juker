namespace Juker.SharedKernel;

/// <summary>Identificador do bar (tenant, ADR-0008).</summary>
/// <param name="Value">O identificador.</param>
public readonly record struct VenueId(Guid Value);
