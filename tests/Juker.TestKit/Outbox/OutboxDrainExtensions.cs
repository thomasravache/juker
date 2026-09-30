namespace Juker.TestKit.Outbox;

/// <summary>
/// Entrega do outbox sob comando nos IT (ADR-0012, regra 6): com o dispatcher desligado, o teste drena o que está
/// pendente de forma síncrona. Interno (InternalsVisibleTo Juker.IntegrationTests) para não alterar a API pública
/// congelada do TestKit (SPEC-0003:CT-01).
/// </summary>
internal static class OutboxDrainExtensions
{
    /// <summary>Resolve o <c>IOutboxDrainer</c> do host e drena até não sobrar pendente; retorna quantas mensagens entregou.</summary>
#pragma warning disable MA0025 // Scaffold da SPEC-0004: o Implementer implementa no Green
    public static Task<int> DrainOutboxAsync(this JukerApiFactory factory) => throw new NotImplementedException();
#pragma warning restore MA0025
}
