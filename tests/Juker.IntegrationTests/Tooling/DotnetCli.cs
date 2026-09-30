using System.Diagnostics;
using System.Text;

namespace Juker.IntegrationTests.Tooling;

/// <summary>Executa o CLI do .NET como processo filho, sempre a partir da raiz do repositório (vale o global.json).</summary>
internal static class DotnetCli
{
    public static async Task<DotnetResult> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
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

        // O processo de teste herda variáveis do MSBuild do `dotnet test`; o build filho deve ser independente.
        var inherited = startInfo.Environment.Keys
            .Where(key => key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var key in inherited)
        {
            startInfo.Environment.Remove(key);
        }

        // Sem nós reaproveitáveis nem servidor de compilação: nada fica segurando a saída depois do build.
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        startInfo.Environment["UseSharedCompilation"] = "false";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

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
