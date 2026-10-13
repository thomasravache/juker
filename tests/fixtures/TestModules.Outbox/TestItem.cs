using Juker.SharedKernel;

namespace TestModules.Outbox;

/// <summary>Entidade <see cref="ITenantScoped"/> de teste que levanta eventos de integração (outbox).</summary>
public sealed class TestItem : Entity<Guid>, ITenantScoped
{
    private TestItem()
    {
    }

    private TestItem(Guid id, VenueId venueId, string name)
        : base(id)
    {
        VenueId = venueId;
        Name = name;
    }

    public VenueId VenueId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Cria o item e levanta <see cref="TestItemCreated"/>.</summary>
    public static TestItem Create(Guid id, VenueId venueId, string name, Guid eventId, DateTimeOffset occurredAt)
    {
        var item = new TestItem(id, venueId, name);
        item.Raise(new TestItemCreated(eventId, occurredAt, id, venueId, name));
        return item;
    }

    /// <summary>Cria o item sem evento próprio (só os eventos publicados depois com <see cref="Publish"/>).</summary>
    public static TestItem Carrier(Guid id, VenueId venueId) => new(id, venueId, "carrier");

    /// <summary>Levanta um evento qualquer, para os cenários do dispatcher.</summary>
    public void Publish(IIntegrationEvent integrationEvent) => Raise(integrationEvent);
}
