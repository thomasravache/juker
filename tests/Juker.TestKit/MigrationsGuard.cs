using Microsoft.EntityFrameworkCore;

namespace Juker.TestKit;

/// <summary>Falha se algum <c>DbContext</c> da solução tiver mudanças no modelo sem migration (ADR-0012, regra 3).</summary>
public static class MigrationsGuard
{
    // Frase verificada pelo SPEC-0003:IT-05 (junto com o nome completo do contexto): não reescrever.
    private const string PendingChanges = "mudanças no modelo sem migration";

    /// <summary>Verifica todos os <c>DbContext</c>; a exceção cita cada contexto com mudanças pendentes.</summary>
    public static void AssertNoPendingModelChanges()
    {
        var pending = new List<string>();
        foreach (var contextType in DbContextDiscovery.ContextTypes())
        {
            using var context = DbContextDiscovery.Create(contextType);
            if (context.Database.HasPendingModelChanges())
            {
                pending.Add(contextType.FullName!);
            }
        }

        if (pending.Count > 0)
        {
            throw new InvalidOperationException(
                $"DbContext com {PendingChanges}: {string.Join(", ", pending)}. " +
                "Crie a migration com `dotnet ef migrations add <Nome>` no projeto do módulo.");
        }
    }
}
