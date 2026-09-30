namespace Juker.ArchitectureTests.Rules;

#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

/// <summary>ARQ-03 — um <c>DbContext</c> por módulo, não compartilhado (ADR-0001, ADR-0004).</summary>
public sealed class DbContextOwnershipRule : IArchitectureRule
{
    public string Id => "ARQ-03";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject) => throw new NotImplementedException();
}
