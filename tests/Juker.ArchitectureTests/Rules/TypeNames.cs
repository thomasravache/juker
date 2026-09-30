using ArchUnitNET.Domain;

namespace Juker.ArchitectureTests.Rules;

/// <summary>Nomes estáveis dos tipos do ArchUnitNET usados nas violações.</summary>
internal static class TypeNames
{
    /// <summary>Nome simples do assembly do tipo (o ArchUnitNET às vezes guarda o nome completo, com versão).</summary>
    public static string AssemblyName(IType type) => SimpleName(type.Assembly?.Name ?? string.Empty);

    /// <summary>Nome simples de um assembly a partir do nome completo (<c>Nome, Version=…</c>).</summary>
    public static string SimpleName(string assemblyName)
    {
        var comma = assemblyName.IndexOf(',', StringComparison.Ordinal);
        return comma >= 0 ? assemblyName[..comma] : assemblyName;
    }

    /// <summary>
    /// O tipo declarado no código-fonte: tipos aninhados e gerados pelo compilador (closures, state machines)
    /// são atribuídos ao tipo externo que os contém.
    /// </summary>
    public static string Offender(IType type)
    {
        var plus = type.FullName.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? type.FullName[..plus] : type.FullName;
    }

    public static string Namespace(IType type) => type.Namespace?.FullName ?? string.Empty;
}
