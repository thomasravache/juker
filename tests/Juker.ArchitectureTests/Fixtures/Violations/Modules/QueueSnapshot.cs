namespace Juker.Modules.Queue.Contracts;

/// <summary>Contrato público do módulo Queue (uso permitido por outros módulos).</summary>
public sealed record QueueSnapshot(Guid VenueId, int Length);
