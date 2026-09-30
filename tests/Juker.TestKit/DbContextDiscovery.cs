using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Juker.TestKit;

/// <summary>
/// Encontra os <see cref="DbContext"/> da solução nos assemblies <c>Juker.*</c> do processo (e nos <c>Juker.*</c>
/// que eles referenciam) e os instancia, na ordem: <see cref="IDesignTimeDbContextFactory{TContext}"/> do mesmo
/// assembly, construtor com <c>DbContextOptions&lt;T&gt;</c> (Npgsql) ou construtor sem parâmetros + <c>OnConfiguring</c>.
/// Usado pelo <see cref="MigrationsGuard"/> e pela <see cref="PostgresFixture"/> (migrations reais do juker_template).
/// </summary>
internal static class DbContextDiscovery
{
    private const string AssemblyPrefix = "Juker.";

    // Sem conexão real: suficiente para montar o modelo e comparar com o snapshot das migrations.
    private const string PlaceholderConnectionString = "Host=localhost;Database=juker_model";

    public static IReadOnlyList<Type> ContextTypes() =>
        JukerAssemblies()
            .SelectMany(LoadableTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false })
            .Where(type => type.IsSubclassOf(typeof(DbContext)))
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

    public static DbContext Create(Type contextType)
    {
        ArgumentNullException.ThrowIfNull(contextType);

        if (FindDesignTimeFactory(contextType) is { } factory)
        {
            var create = factory.GetMethod(nameof(IDesignTimeDbContextFactory<DbContext>.CreateDbContext), [typeof(string[])])!;
            return (DbContext)create.Invoke(Activator.CreateInstance(factory), [Array.Empty<string>()])!;
        }

        var typedOptions = typeof(DbContextOptions<>).MakeGenericType(contextType);
        foreach (var optionsType in new[] { typedOptions, typeof(DbContextOptions) })
        {
            if (contextType.GetConstructor([optionsType]) is { } constructor)
            {
                var builder = (DbContextOptionsBuilder)Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType))!;
                builder.UseNpgsql(PlaceholderConnectionString);
                return (DbContext)constructor.Invoke([builder.Options]);
            }
        }

        if (contextType.GetConstructor(Type.EmptyTypes) is { } parameterless)
        {
            return (DbContext)parameterless.Invoke(null);
        }

        throw new InvalidOperationException(
            $"Não foi possível instanciar o DbContext '{contextType.FullName}': crie um IDesignTimeDbContextFactory<{contextType.Name}>, " +
            $"um construtor com DbContextOptions<{contextType.Name}> ou um construtor sem parâmetros com OnConfiguring.");
    }

    private static Type? FindDesignTimeFactory(Type contextType)
    {
        var factoryInterface = typeof(IDesignTimeDbContextFactory<>).MakeGenericType(contextType);
        return LoadableTypes(contextType.Assembly)
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false })
            .Where(type => type.GetConstructor(Type.EmptyTypes) is not null)
            .FirstOrDefault(factoryInterface.IsAssignableFrom);
    }

    private static List<Assembly> JukerAssemblies()
    {
        var visited = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var pending = new Stack<Assembly>(AppDomain.CurrentDomain.GetAssemblies().Where(IsJuker));
        while (pending.TryPop(out var assembly))
        {
            if (assembly.IsDynamic || !visited.TryAdd(assembly.GetName().Name!, assembly))
            {
                continue;
            }

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name is { } name && name.StartsWith(AssemblyPrefix, StringComparison.Ordinal) && !visited.ContainsKey(name))
                {
                    pending.Push(Assembly.Load(reference));
                }
            }
        }

        return [.. visited.Values];
    }

    private static bool IsJuker(Assembly assembly) =>
        assembly.GetName().Name?.StartsWith(AssemblyPrefix, StringComparison.Ordinal) == true;

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
