using Juker.Infrastructure;
using Juker.SharedKernel;

namespace TestModules.Outbox;

/// <summary>Comando de teste do IT-01: grava um <see cref="TestItem"/>, que levanta <see cref="TestItemCreated"/>.</summary>
public sealed record CreateTestItem(Guid ItemId, VenueId Venue, string Name, Guid EventId, DateTimeOffset OccurredAt) : ICommand<Guid>;
