namespace Juker.ArchitectureTests.Rules;

#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

/// <summary>ARQ-06 — sem MediatR, MassTransit e FluentAssertions (ADR-0002, ADR-0011).</summary>
public sealed class ForbiddenDependenciesRule : IArchitectureRule
{
    public string Id => "ARQ-06";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject) => throw new NotImplementedException();
}
