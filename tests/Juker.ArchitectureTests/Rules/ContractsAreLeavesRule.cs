namespace Juker.ArchitectureTests.Rules;

/// <summary>ARQ-02 — <c>.Contracts</c> não referencia nenhum interno de módulo (ADR-0001).</summary>
public sealed class ContractsAreLeavesRule : IArchitectureRule
{
    // Único código Juker permitido num .Contracts além dos próprios contratos.
    private const string SharedKernel = "Juker.SharedKernel";

    public string Id => "ARQ-02";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var contracts = subject.ProductionTypes.Where(type => JukerNamespaces.IsContracts(TypeNames.Namespace(type)));
        return DependencyScan.Find(Id, contracts, (_, target) =>
        {
            var targetNamespace = TypeNames.Namespace(target);
            return JukerNamespaces.IsJuker(targetNamespace)
                && !JukerNamespaces.IsContracts(targetNamespace)
                && !JukerNamespaces.IsWithin(targetNamespace, SharedKernel)
                    ? $"o .Contracts depende de {target.FullName}; contratos só referenciam outros .Contracts e o {SharedKernel}"
                    : null;
        });
    }
}
