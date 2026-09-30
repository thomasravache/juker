namespace Juker.ArchitectureTests.Rules;

#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

/// <summary>ARQ-05 — <c>src/Web/*</c> só referencia <c>.Contracts</c> dos módulos (ADR-0001).</summary>
public sealed class WebBoundariesRule : IArchitectureRule
{
    public string Id => "ARQ-05";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject) => throw new NotImplementedException();
}
