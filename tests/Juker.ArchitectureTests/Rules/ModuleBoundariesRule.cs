namespace Juker.ArchitectureTests.Rules;

/// <summary>ARQ-01 — um módulo só referencia o <c>.Contracts</c> de outro módulo (ADR-0001).</summary>
public sealed class ModuleBoundariesRule : IArchitectureRule
{
    public string Id => "ARQ-01";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return DependencyScan.Find(Id, subject.ProductionTypes, (origin, target) =>
        {
            var from = JukerNamespaces.ModuleOf(TypeNames.Namespace(origin));
            var targetNamespace = TypeNames.Namespace(target);
            var to = JukerNamespaces.ModuleOf(targetNamespace);
            return from is not null && to is not null && !string.Equals(from, to, StringComparison.Ordinal) && !JukerNamespaces.IsContracts(targetNamespace)
                ? $"o módulo {from} depende de {target.FullName}, interno do módulo {to}; use Juker.Modules.{to}.Contracts"
                : null;
        });
    }
}
