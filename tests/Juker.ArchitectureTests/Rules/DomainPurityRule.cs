namespace Juker.ArchitectureTests.Rules;

#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

/// <summary>ARQ-04 — <c>Domain/</c> sem EF Core, ASP.NET Core nem Infrastructure (ADR-0001).</summary>
public sealed class DomainPurityRule : IArchitectureRule
{
    public string Id => "ARQ-04";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject) => throw new NotImplementedException();
}
