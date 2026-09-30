namespace Juker.ArchitectureTests.Rules;

#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

/// <summary>ARQ-01 — um módulo só referencia o <c>.Contracts</c> de outro módulo (ADR-0001).</summary>
public sealed class ModuleBoundariesRule : IArchitectureRule
{
    public string Id => "ARQ-01";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject) => throw new NotImplementedException();
}
