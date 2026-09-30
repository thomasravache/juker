namespace Juker.ArchitectureTests.Conventions;

/// <summary>Um método de teste fora da convenção de traits.</summary>
/// <param name="Method">Identificação do método: <c>{Tipo.FullName}.{Método}</c>.</param>
/// <param name="Reason">O que falta ou está errado (trait Spec ausente/fora do formato, Category ausente/não permitida).</param>
public sealed record ConventionViolation(string Method, string Reason);
