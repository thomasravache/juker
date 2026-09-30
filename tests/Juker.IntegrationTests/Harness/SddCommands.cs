using System.Text.RegularExpressions;

namespace Juker.IntegrationTests.Harness;

/// <summary>Lê os comandos de teste do <c>docs/specs/sdd-config.yml</c> (os comandos dos gates).</summary>
internal static partial class SddCommands
{
    public static string? Read(string key)
    {
        var config = Tooling.RepositoryPaths.Combine("docs", "specs", "sdd-config.yml");
        foreach (var line in File.ReadLines(config))
        {
            var match = CommandLine().Match(line);
            if (match.Success && string.Equals(match.Groups["key"].Value, key, StringComparison.Ordinal))
            {
                return match.Groups["value"].Value.Replace("\\\"", "\"", StringComparison.Ordinal);
            }
        }

        return null;
    }

    /// <summary>Separa um comando em argumentos, respeitando aspas duplas (que são removidas).</summary>
    public static IReadOnlyList<string> Tokenize(string command)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var character in command)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == ' ' && !quoted)
            {
                Flush();
            }
            else
            {
                current.Append(character);
            }
        }

        Flush();
        return tokens;

        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }
    }

    [GeneratedRegex("""^(?<key>[a-z_]+):\s*"(?<value>(?:[^"\\]|\\.)*)"(?:\s*#.*)?$""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex CommandLine();
}
