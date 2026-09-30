namespace Juker.ArchitectureTests.Rules;

/// <summary>ARQ-07 — tipos YouTube só em <c>Juker.Modules.Catalog.Infrastructure</c> e <c>Juker.Web.Client.Tv</c> (ADR-0007).</summary>
/// <remarks>
/// "Tipo YouTube" é qualquer tipo cujo nome completo contenha <c>YouTube</c> (tipos próprios do provedor ou do SDK
/// do Google). Fora dos dois lugares permitidos, nenhum tipo pode ser um tipo YouTube nem depender de um.
/// </remarks>
public sealed class MediaProviderIsolationRule : IArchitectureRule
{
    private const string Provider = "YouTube";

    private static readonly string[] Allowed = ["Juker.Modules.Catalog.Infrastructure", "Juker.Web.Client.Tv"];

    public string Id => "ARQ-07";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var outside = subject.ProductionTypes
            .Where(type => !Allowed.Any(prefix => JukerNamespaces.IsWithin(TypeNames.Namespace(type), prefix)))
            .ToList();

        var declared = outside
            .Where(type => IsProviderType(type.FullName))
            .Select(type => new RuleViolation(Id, TypeNames.Offender(type), $"tipo do provedor {Provider} fora de {string.Join(" e ", Allowed)}"));

        var used = DependencyScan.Find(Id, outside, (_, target) =>
            IsProviderType(target.FullName) ? $"depende de {target.FullName}, tipo do provedor {Provider} (ADR-0007)" : null);

        return [.. declared.Concat(used).Distinct()];
    }

    private static bool IsProviderType(string fullName) => fullName.Contains(Provider, StringComparison.OrdinalIgnoreCase);
}
