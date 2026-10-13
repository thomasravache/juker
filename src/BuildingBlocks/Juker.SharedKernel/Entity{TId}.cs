namespace Juker.SharedKernel;

/// <summary>Entidade com identidade; acumula os eventos de integração levantados (<see cref="Raise"/>) para o outbox.</summary>
/// <typeparam name="TId">Tipo do identificador.</typeparam>
public abstract class Entity<TId>
    where TId : notnull
{
    private readonly List<IIntegrationEvent> _integrationEvents = [];

    protected Entity()
    {
        Id = default!;
    }

    protected Entity(TId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    public TId Id { get; protected init; }

    public IReadOnlyCollection<IIntegrationEvent> IntegrationEvents => _integrationEvents.AsReadOnly();

    public void ClearIntegrationEvents() => _integrationEvents.Clear();

    protected void Raise(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _integrationEvents.Add(integrationEvent);
    }
}
