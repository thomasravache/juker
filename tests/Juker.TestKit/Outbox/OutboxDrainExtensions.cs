using Juker.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Juker.TestKit.Outbox;

/// <summary>
/// Entrega do outbox sob comando nos IT (ADR-0012, regra 6): com o dispatcher desligado, o teste drena o que está
/// pendente de forma síncrona. Interno (InternalsVisibleTo Juker.IntegrationTests) para não alterar a API pública
/// congelada do TestKit (SPEC-0003:CT-01).
/// </summary>
internal static class OutboxDrainExtensions
{
    /// <summary>Resolve o <c>IOutboxDrainer</c> do host e drena até não sobrar pendente; retorna quantas mensagens entregou.</summary>
    public static Task<int> DrainOutboxAsync(this JukerApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var drainer = factory.Services.GetRequiredService<IOutboxDrainer>();
        return drainer.DrainAsync(CancellationToken.None);
    }
}
