namespace Juker.ArchitectureTests.Rules;

/// <summary>ARQ-03 — um <c>DbContext</c> por módulo, não compartilhado (ADR-0001, ADR-0004).</summary>
/// <remarks>
/// Viola: um <c>DbContext</c> fora de um módulo; mais de um <c>DbContext</c> no mesmo módulo (todos são citados);
/// e o <c>DbContext</c> de um módulo usado por outro módulo ou pelo Web. O host pode usá-lo para compor (migrations).
/// </remarks>
public sealed class DbContextOwnershipRule : IArchitectureRule
{
    private const string DbContextType = "Microsoft.EntityFrameworkCore.DbContext";
    private const string Web = "Juker.Web";

    public string Id => "ARQ-03";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        // Herança por reflexão: o ArchUnitNET não resolve a classe base quando ela está num assembly não carregado.
        var contexts = ArchitectureSubject.ReflectedTypes(subject.ProductionAssemblies)
            .Where(type => type is { IsClass: true, IsAbstract: false } && InheritsDbContext(type))
            .Select(type => (Name: type.FullName!, Module: JukerNamespaces.ModuleOf(type.Namespace ?? string.Empty)))
            .DistinctBy(context => context.Name, StringComparer.Ordinal)
            .ToList();

        var outsideModules = contexts
            .Where(context => context.Module is null)
            .Select(context => new RuleViolation(Id, context.Name, "DbContext fora de um módulo: cada módulo é dono do seu único DbContext"));

        var duplicated = contexts
            .Where(context => context.Module is not null)
            .GroupBy(context => context.Module!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(context => new RuleViolation(
                Id,
                context.Name,
                $"o módulo {group.Key} tem {group.Count()} DbContext ({string.Join(", ", group.Select(other => other.Name))}); o limite é um")));

        var owners = contexts.Where(context => context.Module is not null)
            .ToDictionary(context => context.Name, context => context.Module!, StringComparer.Ordinal);
        var shared = DependencyScan.Find(Id, subject.ProductionTypes, (origin, target) =>
        {
            if (!owners.TryGetValue(target.FullName, out var owner))
            {
                return null;
            }

            var originNamespace = TypeNames.Namespace(origin);
            var user = JukerNamespaces.ModuleOf(originNamespace);
            var foreign = (user is not null && !string.Equals(user, owner, StringComparison.Ordinal))
                || JukerNamespaces.IsWithin(originNamespace, Web);
            return foreign ? $"usa {target.FullName}, o DbContext do módulo {owner}; o DbContext não é compartilhado" : null;
        });

        return [.. outsideModules, .. duplicated, .. shared];
    }

    private static bool InheritsDbContext(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.FullName, DbContextType, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
