namespace MediatR;

/// <summary>Simula o MediatR (ARQ-06) sem referenciar o pacote comercial.</summary>
public interface IRequest
{
    Guid RequestId { get; }
}
