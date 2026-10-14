namespace Juker.CiTests.Tooling;

/// <summary>Código de saída, stdout e stderr de um processo filho.</summary>
internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string Output => string.Concat(StandardOutput, StandardError);
}
