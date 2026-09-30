namespace Juker.Infrastructure;

/// <summary>Entrega de forma síncrona tudo o que está pendente no outbox de todos os módulos (ADR-0012).</summary>
public interface IOutboxDrainer
{
    /// <summary>Entrega as mensagens pendentes e retorna quantas entregou.</summary>
    Task<int> DrainAsync(CancellationToken ct);
}
