namespace Juker.ArchitectureTests.Conventions;

/// <summary>Um projeto de teste fora da regra de zero testes (SPEC-0027:UT-03).</summary>
/// <param name="Project">Caminho do projeto, relativo à pasta da solução.</param>
/// <param name="Reason">Por que o projeto viola a regra.</param>
public sealed record ZeroTestsViolation(string Project, string Reason);
