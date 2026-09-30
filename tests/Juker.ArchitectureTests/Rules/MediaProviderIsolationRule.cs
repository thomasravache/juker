namespace Juker.ArchitectureTests.Rules;

#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

/// <summary>ARQ-07 — tipos YouTube só em <c>Juker.Modules.Catalog.Infrastructure</c> e <c>Juker.Web.Client.Tv</c> (ADR-0007).</summary>
public sealed class MediaProviderIsolationRule : IArchitectureRule
{
    public string Id => "ARQ-07";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject) => throw new NotImplementedException();
}
