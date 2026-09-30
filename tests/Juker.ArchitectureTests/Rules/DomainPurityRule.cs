namespace Juker.ArchitectureTests.Rules;

/// <summary>ARQ-04 — <c>Domain/</c> sem EF Core, ASP.NET Core nem Infrastructure (ADR-0001).</summary>
public sealed class DomainPurityRule : IArchitectureRule
{
    private const string Domain = "Domain";
    private const string Infrastructure = "Infrastructure";

    private static readonly string[] ForbiddenNamespaces = ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql"];

    public string Id => "ARQ-04";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var domain = subject.ProductionTypes.Where(type =>
            JukerNamespaces.IsJuker(TypeNames.Namespace(type)) && JukerNamespaces.HasSegment(TypeNames.Namespace(type), Domain));
        return DependencyScan.Find(Id, domain, (_, target) =>
        {
            var targetNamespace = TypeNames.Namespace(target);
            var forbidden = ForbiddenNamespaces.Any(prefix => JukerNamespaces.IsWithin(targetNamespace, prefix))
                || (JukerNamespaces.IsJuker(targetNamespace) && JukerNamespaces.HasSegment(targetNamespace, Infrastructure));
            return forbidden ? $"o Domain depende de {target.FullName}; o domínio não conhece EF Core, ASP.NET Core nem Infrastructure" : null;
        });
    }
}
