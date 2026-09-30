namespace Juker.ArchitectureTests.Rules;

/// <summary>
/// Como os namespaces do Juker se mapeiam para módulos e camadas (ADR-0001): <c>Juker.Modules.{Módulo}[.…]</c>,
/// <c>Juker.Modules.{Módulo}.Contracts[.…]</c>, <c>Juker.Web[.…]</c> e pastas <c>Domain</c>/<c>Infrastructure</c>.
/// </summary>
internal static class JukerNamespaces
{
    private const string Root = "Juker";
    private const string Modules = "Modules";
    private const string Contracts = "Contracts";

    public static bool IsJuker(string ns) => IsWithin(ns, Root);

    /// <summary>O módulo dono do namespace (<c>Queue</c> em <c>Juker.Modules.Queue.Domain</c>), ou <c>null</c>.</summary>
    public static string? ModuleOf(string ns)
    {
        var segments = ns.Split('.');
        return segments is [Root, Modules, var module, ..] ? module : null;
    }

    /// <summary>Namespace do <c>.Contracts</c> de algum módulo.</summary>
    public static bool IsContracts(string ns) => ns.Split('.') is [Root, Modules, _, Contracts, ..];

    /// <summary>Namespace interno de algum módulo (qualquer coisa do módulo fora do <c>.Contracts</c>).</summary>
    public static bool IsModuleInternal(string ns) => ModuleOf(ns) is not null && !IsContracts(ns);

    /// <summary>Namespace com o segmento <paramref name="segment"/> (por exemplo <c>Domain</c> ou <c>Infrastructure</c>).</summary>
    public static bool HasSegment(string ns, string segment) => ns.Split('.').Contains(segment, StringComparer.Ordinal);

    /// <summary><paramref name="ns"/> é <paramref name="prefix"/> ou um namespace filho dele.</summary>
    public static bool IsWithin(string ns, string prefix) =>
        string.Equals(ns, prefix, StringComparison.Ordinal) || ns.StartsWith(prefix + ".", StringComparison.Ordinal);
}
