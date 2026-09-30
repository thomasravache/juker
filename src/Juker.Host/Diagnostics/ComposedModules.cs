namespace Juker.Host.Diagnostics;

/// <summary>
/// Registro dos módulos que o host de fato compôs (SPEC-0026), fonte de <c>/_diag/modules</c>.
/// Singleton no DI, preenchido somente por <c>AddJukerModules</c>, com os módulos que ele adicionou.
/// </summary>
public sealed class ComposedModules(IReadOnlyList<string> names)
{
    /// <summary>Nomes dos módulos compostos, na ordem de registro.</summary>
    public IReadOnlyList<string> Names { get; } = names;
}
