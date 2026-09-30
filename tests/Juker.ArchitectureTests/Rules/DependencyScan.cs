using ArchUnitNET.Domain;

namespace Juker.ArchitectureTests.Rules;

/// <summary>Varre as dependências de tipo (ArchUnitNET) e transforma as proibidas em violações, sem repetição.</summary>
internal static class DependencyScan
{
    /// <summary>
    /// Para cada dependência de cada tipo de <paramref name="origins"/>, <paramref name="reasonFor"/> devolve o motivo
    /// da violação (dependência proibida) ou <c>null</c> (permitida).
    /// </summary>
    public static IReadOnlyList<RuleViolation> Find(string rule, IEnumerable<IType> origins, Func<IType, IType, string?> reasonFor) =>
        origins
            .SelectMany(origin => origin.Dependencies
                .SelectMany(dependency => Targets(dependency))
                .Select(target => (Origin: origin, Reason: reasonFor(origin, target))))
            .Where(candidate => candidate.Reason is not null)
            .Select(candidate => new RuleViolation(rule, TypeNames.Offender(candidate.Origin), candidate.Reason!))
            .Distinct()
            .OrderBy(violation => violation.Offender, StringComparer.Ordinal)
            .ThenBy(violation => violation.Reason, StringComparer.Ordinal)
            .ToList();

    /// <summary>O alvo da dependência e os argumentos genéricos dele (<c>List&lt;QueueItem&gt;</c> depende de <c>QueueItem</c>).</summary>
    private static IEnumerable<IType> Targets(ArchUnitNET.Domain.Dependencies.ITypeDependency dependency)
    {
        yield return dependency.Target;
        foreach (var argument in dependency.TargetGenericArguments)
        {
            if (argument.Type is { } type)
            {
                yield return type;
            }
        }
    }
}
