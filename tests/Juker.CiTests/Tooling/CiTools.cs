namespace Juker.CiTests.Tooling;

/// <summary>
/// Ferramentas fixadas por versão e SHA-256 em <c>tools/ci/ci-tools.sh</c> (actionlint 1.7.12 com o shellcheck
/// 0.11.0), baixadas para o cache do usuário na primeira execução, sem sudo.
/// </summary>
internal static class CiTools
{
    private static readonly string Script = Path.Combine("tools", "ci", "ci-tools.sh");

    /// <summary>Roda o actionlint sobre os arquivos (caminhos relativos à raiz), sem cores.</summary>
    public static Task<ProcessResult> ActionlintAsync(IEnumerable<string> files, CancellationToken cancellationToken) =>
        ProcessRunner.RunAsync("bash", [Script, "actionlint", "-no-color", .. files], cancellationToken);
}
