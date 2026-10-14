#pragma warning disable MA0025 // Scaffold do UT-01 (SPEC-0025): corpos lançam NotImplementedException até a implementação
using System.Reflection;

namespace Juker.E2ETests.Conventions;

/// <summary>
/// Convenção "sem espera fixa" dos E2E (SPEC-0025:UT-01, ADR-0013 regra 9): lê o IL dos métodos e aponta os que
/// chamam <c>Thread.Sleep</c> ou <c>Task.Delay</c>, inclusive no corpo de métodos <c>async</c> (máquina de estados)
/// e em lambdas declaradas no método.
/// </summary>
internal static class FixedWaitScanner
{
    /// <summary>Os métodos de teste (<c>[Fact]</c>/<c>[Theory]</c>) declarados no assembly.</summary>
    public static IReadOnlyList<MethodInfo> TestMethods(Assembly assembly) => throw new NotImplementedException();

    /// <summary>
    /// Os métodos (entre <paramref name="methods"/>) que chamam <c>Thread.Sleep</c> ou <c>Task.Delay</c>, como
    /// <c>{Tipo.FullName}.{Método}</c>.
    /// </summary>
    public static IReadOnlyList<string> FindCallers(IEnumerable<MethodInfo> methods) => throw new NotImplementedException();
}
