namespace Juker.SharedKernel;

/// <summary>Entidade com identidade; acumula os eventos de integração levantados (<see cref="Raise"/>) para o outbox.</summary>
/// <typeparam name="TId">Tipo do identificador.</typeparam>
public abstract class Entity<TId>
    where TId : notnull
{
    protected Entity() => throw new NotImplementedException();

    protected Entity(TId id) => throw new NotImplementedException();

    public TId Id
    {
        get => throw new NotImplementedException();
        protected init => throw new NotImplementedException();
    }

    public IReadOnlyCollection<IIntegrationEvent> IntegrationEvents => throw new NotImplementedException();

    public void ClearIntegrationEvents() => throw new NotImplementedException();

    protected void Raise(IIntegrationEvent integrationEvent) => throw new NotImplementedException();
}
