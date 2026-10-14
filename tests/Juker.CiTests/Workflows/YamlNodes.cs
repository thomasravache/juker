using YamlDotNet.RepresentationModel;

namespace Juker.CiTests.Workflows;

/// <summary>Navegação tolerante no modelo de representação do YamlDotNet (nós ausentes viram <c>null</c>/vazio).</summary>
internal static class YamlNodes
{
    public static YamlMappingNode LoadMapping(string path)
    {
        var stream = new YamlStream();
        using (var reader = new StreamReader(path))
        {
            stream.Load(reader);
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidDataException($"'{path}' não é um documento YAML com um mapa na raiz.");
        }

        return root;
    }

    /// <summary>O valor da chave, se o nó for um mapa que a contém.</summary>
    public static YamlNode? Child(this YamlNode? node, string key) =>
        node is YamlMappingNode mapping && mapping.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;

    public static YamlMappingNode? Mapping(this YamlNode? node, string key) => node.Child(key) as YamlMappingNode;

    /// <summary>O texto de um escalar (ou <c>null</c> se o nó não for escalar).</summary>
    public static string? Text(this YamlNode? node) => (node as YamlScalarNode)?.Value;

    public static string? Text(this YamlNode? node, string key) => node.Child(key).Text();

    /// <summary>Os itens de uma sequência; um escalar sozinho conta como sequência de um item (<c>needs: build</c>).</summary>
    public static IReadOnlyList<YamlNode> Items(this YamlNode? node) => node switch
    {
        YamlSequenceNode sequence => [.. sequence.Children],
        YamlScalarNode scalar => [scalar],
        _ => [],
    };

    /// <summary>Os textos dos itens escalares (<see cref="Items"/>).</summary>
    public static IReadOnlyList<string> Texts(this YamlNode? node) =>
        [.. node.Items().Select(item => item.Text()).OfType<string>()];

    /// <summary>As entradas de um mapa com chave escalar.</summary>
    public static IReadOnlyList<KeyValuePair<string, YamlNode>> Entries(this YamlNode? node) => node is YamlMappingNode mapping
        ? [.. mapping.Children
            .Where(entry => entry.Key is YamlScalarNode { Value: not null })
            .Select(entry => new KeyValuePair<string, YamlNode>(((YamlScalarNode)entry.Key).Value!, entry.Value))]
        : [];

    /// <summary>Todos os escalares da subárvore (chaves e valores), em ordem de documento.</summary>
    public static IEnumerable<string> AllScalars(this YamlNode? node)
    {
        switch (node)
        {
            case YamlScalarNode { Value: { } value }:
                yield return value;
                break;
            case YamlSequenceNode sequence:
                foreach (var scalar in sequence.Children.SelectMany(child => child.AllScalars()))
                {
                    yield return scalar;
                }

                break;
            case YamlMappingNode mapping:
                foreach (var entry in mapping.Children)
                {
                    foreach (var scalar in entry.Key.AllScalars().Concat(entry.Value.AllScalars()))
                    {
                        yield return scalar;
                    }
                }

                break;
        }
    }
}
