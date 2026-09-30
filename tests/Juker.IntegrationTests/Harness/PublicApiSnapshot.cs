using System.Reflection;
using System.Text;

namespace Juker.IntegrationTests.Harness;

/// <summary>
/// Texto estável da API pública de um assembly: tipos exportados, base, interfaces próprias e membros públicos
/// declarados (sem overrides nem acessores), para comparar com o snapshot aprovado do contrato.
/// </summary>
internal static class PublicApiSnapshot
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static string Render(Assembly assembly)
    {
        var text = new StringBuilder();
        foreach (var type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            text.Append(Header(type)).Append('\n');
            foreach (var member in Members(type).Order(StringComparer.Ordinal))
            {
                text.Append("  ").Append(member).Append('\n');
            }
        }

        return text.ToString();
    }

    private static string Header(Type type)
    {
        var kind = type switch
        {
            { IsInterface: true } => "interface",
            { IsEnum: true } => "enum",
            { IsValueType: true } => "struct",
            { IsAbstract: true, IsSealed: true } => "static class",
            { IsSealed: true } => "sealed class",
            { IsAbstract: true } => "abstract class",
            _ => "class",
        };

        var inherited = type.BaseType?.GetInterfaces() ?? [];
        var bases = new List<string>();
        if (type.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType))
        {
            bases.Add(Name(baseType));
        }

        bases.AddRange(type.GetInterfaces().Except(inherited).Select(Name).Order(StringComparer.Ordinal));
        return bases.Count == 0 ? $"{kind} {Name(type)}" : $"{kind} {Name(type)} : {string.Join(", ", bases)}";
    }

    private static IEnumerable<string> Members(Type type)
    {
        foreach (var constructor in type.GetConstructors(Declared))
        {
            yield return $"ctor({Parameters(constructor)})";
        }

        foreach (var property in type.GetProperties(Declared))
        {
            var accessors = new List<string>();
            if (property.GetMethod is { IsPublic: true })
            {
                accessors.Add("get;");
            }

            if (property.SetMethod is { IsPublic: true })
            {
                accessors.Add("set;");
            }

            yield return $"property {Static(property.GetMethod ?? property.SetMethod)}{Name(property.PropertyType)} {property.Name} {{ {string.Join(' ', accessors)} }}";
        }

        foreach (var method in type.GetMethods(Declared).Where(method => !method.IsSpecialName && method.GetBaseDefinition() == method))
        {
            yield return $"method {Static(method)}{Name(method.ReturnType)} {method.Name}({Parameters(method)})";
        }

        foreach (var field in type.GetFields(Declared))
        {
            yield return $"field {(field.IsStatic ? "static " : string.Empty)}{Name(field.FieldType)} {field.Name}";
        }

        foreach (var @event in type.GetEvents(Declared))
        {
            yield return $"event {Name(@event.EventHandlerType!)} {@event.Name}";
        }
    }

    private static string Static(MethodInfo? method) => method is { IsStatic: true } ? "static " : string.Empty;

    private static string Parameters(MethodBase method) =>
        string.Join(", ", method.GetParameters().Select(parameter => Name(parameter.ParameterType)));

    private static string Name(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var definition = type.GetGenericTypeDefinition().FullName!;
        var name = definition[..definition.IndexOf('`', StringComparison.Ordinal)];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(Name))}>";
    }
}
