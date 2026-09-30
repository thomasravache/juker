namespace Juker.Modules.Venues.Features.Register;

/// <summary>ARQ-06: um tipo Juker implementa uma interface do MediatR.</summary>
public sealed class RegisterVenueRequest : MediatR.IRequest
{
    public Guid RequestId { get; init; }
}
