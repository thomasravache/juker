namespace Juker.ArchitectureTests.Rules;

/// <summary>Uma regra de arquitetura avaliada sobre um conjunto de assemblies.</summary>
public interface IArchitectureRule
{
    /// <summary>Identificador da regra (ARQ-01..ARQ-08, SPEC-0001).</summary>
    string Id { get; }

    /// <summary>Avalia a regra e devolve todas as violações (vazio quando a regra é respeitada).</summary>
    IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject);
}
