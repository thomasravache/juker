namespace Juker.ArchitectureTests.Rules;

/// <summary>ARQ-05 — <c>src/Web/*</c> só referencia <c>.Contracts</c> dos módulos (ADR-0001).</summary>
public sealed class WebBoundariesRule : IArchitectureRule
{
    private const string Web = "Juker.Web";

    // Além dos internos dos módulos, o Web não conhece a infraestrutura nem o host que o compõe.
    private static readonly string[] ForbiddenNamespaces = ["Juker.Infrastructure", "Juker.Host"];

    public string Id => "ARQ-05";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var web = subject.ProductionTypes.Where(type => JukerNamespaces.IsWithin(TypeNames.Namespace(type), Web));
        return DependencyScan.Find(Id, web, (_, target) =>
        {
            var targetNamespace = TypeNames.Namespace(target);
            var forbidden = JukerNamespaces.IsModuleInternal(targetNamespace)
                || ForbiddenNamespaces.Any(prefix => JukerNamespaces.IsWithin(targetNamespace, prefix));
            return forbidden ? $"o Web depende de {target.FullName}; o Web só usa os .Contracts dos módulos" : null;
        });
    }
}
