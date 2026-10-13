using Juker.SharedKernel;

namespace Juker.BuildingBlocks.UnitTests.Tenancy;

/// <summary>Entidade <see cref="ITenantScoped"/> de teste do UT-04.</summary>
public sealed class TenantItem : Entity<Guid>, ITenantScoped
{
    private TenantItem()
    {
    }

    public VenueId VenueId { get; private set; }

    public string Name { get; private set; } = string.Empty;
}
