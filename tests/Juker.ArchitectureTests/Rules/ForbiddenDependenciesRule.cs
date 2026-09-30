namespace Juker.ArchitectureTests.Rules;

/// <summary>ARQ-06 — sem MediatR, MassTransit e FluentAssertions (ADR-0002, ADR-0011).</summary>
/// <remarks>
/// Vale para produção e testes. FluentAssertions é proibido em qualquer versão (a verificação por namespace e por
/// nome de assembly não distingue versões; o Juker usa AwesomeAssertions).
/// </remarks>
public sealed class ForbiddenDependenciesRule : IArchitectureRule
{
    private static readonly string[] Forbidden = ["MediatR", "MassTransit", "FluentAssertions"];

    public string Id => "ARQ-06";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var byType = DependencyScan.Find(
            Id,
            subject.AllTypes.Where(type => ForbiddenLibrary(TypeNames.Namespace(type)) is null),
            (_, target) => ForbiddenLibrary(TypeNames.Namespace(target)) is { } library
                ? $"depende de {target.FullName} ({library}, proibido pelo ADR-0002/ADR-0011)"
                : null);

        // Referência de assembly sem uso visível por tipo (por exemplo, só em atributos do assembly).
        var byAssembly = subject.ProductionAssemblies.Concat(subject.TestAssemblies)
            .DistinctBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
            .SelectMany(assembly => assembly.GetReferencedAssemblies()
                .Where(reference => ForbiddenLibrary(reference.Name ?? string.Empty) is not null)
                .Select(reference => new RuleViolation(
                    Id,
                    assembly.GetName().Name!,
                    $"referencia o assembly {reference.Name} (proibido pelo ADR-0002/ADR-0011)")));

        return [.. byType, .. byAssembly];
    }

    private static string? ForbiddenLibrary(string name) =>
        Forbidden.FirstOrDefault(library => JukerNamespaces.IsWithin(name, library));
}
