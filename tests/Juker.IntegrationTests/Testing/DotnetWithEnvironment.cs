using System.Diagnostics;
using System.Text;
using Juker.IntegrationTests.Tooling;

namespace Juker.IntegrationTests.Testing;

/// <summary>
/// Executa o CLI do .NET a partir da raiz do repositório com variáveis de ambiente próprias do processo filho
/// (ex.: <c>JUKER_E2E_BASE_URL</c> no SPEC-0025:CT-01), sem mexer no ambiente deste processo. Mesmo isolamento do
/// <see cref="DotnetCli"/>: sem as variáveis do MSBuild herdadas, sem reaproveitar nós nem servidor de compilação.
/// </summary>
internal static class DotnetWithEnvironment
{
    public static async Task<DotnetResult> RunAsync(
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environment);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepositoryPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var inherited = startInfo.Environment.Keys
            .Where(key => key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var key in inherited)
        {
            startInfo.Environment.Remove(key);
        }

        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        startInfo.Environment["UseSharedCompilation"] = "false";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
        }

        var output = new StringBuilder();
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => Append(output, e.Data);
        process.ErrorDataReceived += (_, e) => Append(output, e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        lock (output)
        {
            return new DotnetResult(process.ExitCode, output.ToString());
        }
    }

    private static void Append(StringBuilder output, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(line);
        }
    }
}
