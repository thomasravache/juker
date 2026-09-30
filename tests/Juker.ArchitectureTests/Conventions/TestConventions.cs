using System.Reflection;
using System.Text.RegularExpressions;
using Juker.ArchitectureTests.Rules;

namespace Juker.ArchitectureTests.Conventions;

/// <summary>
/// Convenção de rastreabilidade (SPEC-0003, ADR-0011): todo <c>[Fact]</c>/<c>[Theory]</c> tem um trait
/// <c>Spec</c> no formato <c>SPEC-NNNN:(CH|UT|IT|CT|E2E)-NN</c> e um trait <c>Category</c> permitido
/// (<c>Unit</c>, <c>Integration</c>, <c>E2E</c>, <c>Smoke</c>, <c>Architecture</c>), no método ou na classe.
/// </summary>
/// <remarks>
/// Os atributos são lidos por nome (<see cref="CustomAttributeData"/>): os assemblies de teste podem estar em outros
/// contextos de carga, com a própria cópia do xUnit.
/// </remarks>
public static partial class TestConventions
{
    private const string FactAttribute = "Xunit.FactAttribute";
    private const string FactAttributeContract = "Xunit.v3.IFactAttribute";
    private const string TraitAttribute = "Xunit.TraitAttribute";
    private const string SpecTrait = "Spec";
    private const string CategoryTrait = "Category";

    private const BindingFlags DeclaredMethods =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly string[] AllowedCategories = ["Unit", "Integration", "E2E", "Smoke", "Architecture"];

    /// <summary>Os assemblies de teste reais da solução (projetos de teste da Juker.slnx), sem fixtures.</summary>
    public static IReadOnlyList<Assembly> SolutionTestAssemblies() => SolutionAssemblies.Tests;

    /// <summary>Todos os métodos de teste dos assemblies informados que violam a convenção.</summary>
    public static IReadOnlyList<ConventionViolation> FindViolations(IEnumerable<Assembly> testAssemblies)
    {
        ArgumentNullException.ThrowIfNull(testAssemblies);

        var violations = new List<ConventionViolation>();
        foreach (var type in ArchitectureSubject.ReflectedTypes(testAssemblies))
        {
            foreach (var method in type.GetMethods(DeclaredMethods).Where(IsTest))
            {
                var traits = Traits(method, type);
                var name = $"{type.FullName}.{method.Name}";
                violations.AddRange(Check(name, SpecTrait, traits, IsValidSpec, "fora do formato SPEC-NNNN:(CH|UT|IT|CT|E2E)-NN"));
                violations.AddRange(Check(name, CategoryTrait, traits, IsAllowedCategory, $"não permitida (use {string.Join(", ", AllowedCategories)})"));
            }
        }

        return violations;
    }

    private static IEnumerable<ConventionViolation> Check(
        string method,
        string trait,
        ILookup<string, string> traits,
        Func<string, bool> isValid,
        string invalidReason)
    {
        var values = traits[trait].ToList();
        if (values.Count == 0)
        {
            yield return new ConventionViolation(method, $"sem [Trait(\"{trait}\", …)] no método nem na classe");
            yield break;
        }

        foreach (var value in values.Where(value => !isValid(value)))
        {
            yield return new ConventionViolation(method, $"{trait} '{value}' {invalidReason}");
        }
    }

    private static bool IsTest(MethodInfo method) =>
        method.CustomAttributes.Any(attribute => IsFactAttribute(attribute.AttributeType));

    private static bool IsFactAttribute(Type attributeType)
    {
        for (var current = attributeType; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.FullName, FactAttribute, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return attributeType.GetInterfaces().Any(contract => string.Equals(contract.FullName, FactAttributeContract, StringComparison.Ordinal));
    }

    /// <summary>Traits do método e da classe (incluindo as classes base, como o xUnit).</summary>
    private static ILookup<string, string> Traits(MethodInfo method, Type type)
    {
        var classAttributes = new List<CustomAttributeData>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            classAttributes.AddRange(current.CustomAttributes);
        }

        return method.CustomAttributes.Concat(classAttributes)
            .Where(attribute => string.Equals(attribute.AttributeType.FullName, TraitAttribute, StringComparison.Ordinal))
            .Where(attribute => attribute.ConstructorArguments is [{ Value: string }, { Value: string }])
            .ToLookup(
                attribute => (string)attribute.ConstructorArguments[0].Value!,
                attribute => (string)attribute.ConstructorArguments[1].Value!,
                StringComparer.Ordinal);
    }

    private static bool IsValidSpec(string value) => SpecFormat().IsMatch(value);

    private static bool IsAllowedCategory(string value) => AllowedCategories.Contains(value, StringComparer.Ordinal);

    [GeneratedRegex(@"^SPEC-\d{4}:(CH|UT|IT|CT|E2E)-\d{2}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex SpecFormat();
}
