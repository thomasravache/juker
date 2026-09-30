#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

namespace Juker.TestKit;

/// <summary>Falha se algum <c>DbContext</c> da solução tiver mudanças no modelo sem migration (ADR-0012, regra 3).</summary>
public static class MigrationsGuard
{
    /// <summary>Verifica todos os <c>DbContext</c>; a exceção cita cada contexto com mudanças pendentes.</summary>
    public static void AssertNoPendingModelChanges() => throw new NotImplementedException();
}
