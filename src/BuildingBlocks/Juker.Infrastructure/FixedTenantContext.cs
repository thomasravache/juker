using Juker.SharedKernel;

namespace Juker.Infrastructure;

/// <summary><see cref="ITenantContext"/> explícito, para jobs e testes.</summary>
public sealed class FixedTenantContext : ITenantContext
{
    /// <param name="venueId">O bar do contexto, ou <c>null</c> para nenhum.</param>
    public FixedTenantContext(VenueId? venueId)
    {
        VenueId = venueId;
    }

    public VenueId? VenueId { get; }
}
