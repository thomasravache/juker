using System.Text.RegularExpressions;
using Juker.CiTests.Tooling;
using YamlDotNet.RepresentationModel;

namespace Juker.CiTests.Workflows;

/// <summary>Um workflow do GitHub Actions lido de <c>.github/workflows/</c>.</summary>
internal sealed partial class Workflow
{
    public const string CiFile = "ci.yml";
    public const string MutationFile = "mutation.yml";

    private Workflow(string relativePath, YamlMappingNode root)
    {
        RelativePath = relativePath;
        Root = root;
    }

    /// <summary>Caminho relativo à raiz do repositório, com <c>/</c>.</summary>
    public string RelativePath { get; }

    public YamlMappingNode Root { get; }

    /// <summary>Os gatilhos (<c>on:</c>); a forma em lista (<c>on: [push]</c>) vira um mapa sem configuração.</summary>
    public IReadOnlyDictionary<string, YamlNode?> Triggers
    {
        get
        {
            var on = Root.Child("on");
            return on is YamlMappingNode
                ? on.Entries().ToDictionary(entry => entry.Key, entry => (YamlNode?)entry.Value, StringComparer.Ordinal)
                : on.Texts().ToDictionary(name => name, _ => (YamlNode?)null, StringComparer.Ordinal);
        }
    }

    public IReadOnlyDictionary<string, YamlMappingNode> Jobs =>
        Root.Child("jobs").Entries()
            .Where(entry => entry.Value is YamlMappingNode)
            .ToDictionary(entry => entry.Key, entry => (YamlMappingNode)entry.Value, StringComparer.Ordinal);

    public static string WorkflowsDirectory => RepositoryPaths.Combine(".github", "workflows");

    public static string PathOf(string fileName) => Path.Combine(WorkflowsDirectory, fileName);

    public static bool Exists(string fileName) => File.Exists(PathOf(fileName));

    public static Workflow Load(string fileName) =>
        new($".github/workflows/{fileName}", YamlNodes.LoadMapping(PathOf(fileName)));

    /// <summary>Todos os workflows do repositório (<c>*.yml</c> e <c>*.yaml</c>), em ordem de nome.</summary>
    public static IReadOnlyList<string> AllFileNames() => Directory.Exists(WorkflowsDirectory)
        ? [.. Directory.EnumerateFiles(WorkflowsDirectory)
            .Where(file => file.EndsWith(".yml", StringComparison.Ordinal) || file.EndsWith(".yaml", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)]
        : [];

    public static IReadOnlyList<YamlMappingNode> Steps(YamlMappingNode job) =>
        [.. job.Child("steps").Items().OfType<YamlMappingNode>()];

    /// <summary>
    /// Os scripts <c>run:</c> do job, juntos e normalizados para comparação: continuações de linha removidas,
    /// aspas retiradas e espaços colapsados.
    /// </summary>
    public static string RunText(YamlMappingNode job) =>
        NormalizeCommand(string.Join('\n', Steps(job).Select(step => step.Text("run")).OfType<string>()));

    public static string NormalizeCommand(string text) =>
        Whitespace().Replace(text.Replace("\\\n", " ", StringComparison.Ordinal).Replace("\"", string.Empty, StringComparison.Ordinal).Replace("'", string.Empty, StringComparison.Ordinal), " ").Trim();

    /// <summary>Os passos que usam a action informada (<c>owner/repo</c> ou <c>owner/repo/caminho</c>), em qualquer versão.</summary>
    public static IEnumerable<YamlMappingNode> StepsUsing(YamlMappingNode job, string action) =>
        Steps(job).Where(step => step.Text("uses") is { } uses && UsesAction(uses, action));

    /// <summary>Os passos <c>actions/upload-artifact</c> de todos os jobs, com o id do job.</summary>
    public IEnumerable<(string Job, YamlMappingNode Step)> UploadSteps() =>
        Jobs.SelectMany(job => StepsUsing(job.Value, "actions/upload-artifact").Select(step => (job.Key, step)));

    /// <summary>Todos os <c>uses:</c> (de passos e de jobs que chamam workflows reutilizáveis).</summary>
    public IEnumerable<string> AllUses() =>
        Jobs.Values.SelectMany(job => Steps(job).Select(step => step.Text("uses")).Append(job.Text("uses"))).OfType<string>();

    public static bool UsesAction(string uses, string action)
    {
        var at = uses.IndexOf('@', StringComparison.Ordinal);
        var name = at < 0 ? uses : uses[..at];
        return string.Equals(name, action, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex Whitespace();
}
