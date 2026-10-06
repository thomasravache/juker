using System.Diagnostics.CodeAnalysis;
using Juker.SharedKernel;

namespace Juker.Infrastructure;

/// <summary>Consumidor de um evento de integração; idempotente pelo inbox do módulo (ADR-0006).</summary>
/// <typeparam name="TEvent">O evento consumido.</typeparam>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Nome do contrato (SPEC-0004, seção 6); não é um delegate de evento .NET.")]
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken ct);
}
