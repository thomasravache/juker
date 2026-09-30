namespace Juker.SharedKernel;

/// <summary>Entidade de um bar: sujeita ao filtro global de tenant do <c>ModuleDbContext</c> (ADR-0008).</summary>
public interface ITenantScoped
{
    VenueId VenueId { get; }
}
