using System.Reflection;

namespace Juker.IntegrationTests.Harness;

/// <summary>
/// As migrations reais da solução: tipos com <c>[Migration("id")]</c> nos assemblies <c>Juker.*</c> alcançáveis a
/// partir do <c>Juker.Host</c>. Lido por reflexão, sem depender de como o TestKit as aplica.
/// </summary>
internal static class RealMigrations
{
    private const string MigrationAttribute = "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute";

    public static IReadOnlyList<string> Ids()
    {
        var ids = new List<string>();
        foreach (var assembly in JukerAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                var migration = type.CustomAttributes.FirstOrDefault(attribute =>
                    string.Equals(attribute.AttributeType.FullName, MigrationAttribute, StringComparison.Ordinal));
                if (migration?.ConstructorArguments is [{ Value: string id }])
                {
                    ids.Add(id);
                }
            }
        }

        return ids.Order(StringComparer.Ordinal).ToList();
    }

    private static List<Assembly> JukerAssemblies()
    {
        var visited = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var pending = new Stack<Assembly>([typeof(Juker.Host.ModuleCatalog).Assembly]);
        while (pending.TryPop(out var assembly))
        {
            if (!visited.TryAdd(assembly.GetName().Name!, assembly))
            {
                continue;
            }

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name is { } name
                    && name.StartsWith("Juker.", StringComparison.Ordinal)
                    && !visited.ContainsKey(name))
                {
                    pending.Push(Assembly.Load(reference));
                }
            }
        }

        return [.. visited.Values];
    }
}
