using Juker.SharedKernel;

namespace TestModules.Outbox;

/// <summary>Evento de integração de teste. Levantado por TestItem.Create (IT-01).</summary>
public sealed record TestItemCreated(Guid Id, DateTimeOffset OccurredAt, Guid ItemId, VenueId Venue, string Name) : IIntegrationEvent;
