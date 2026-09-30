using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;

namespace Juker.ArchitectureTests.Rules;

/// <summary>ARQ-08 — testes sem EF Core InMemory/Sqlite nem mocks de <c>HttpMessageHandler</c> (ADR-0012).</summary>
/// <remarks>
/// Viola:
/// <list type="bullet">
/// <item>usar tipos do EF Core InMemory/Sqlite ou de bibliotecas de mock HTTP;</item>
/// <item>escrever uma subclasse de <c>HttpMessageHandler</c> (mock escrito à mão);</item>
/// <item>passar <c>HttpMessageHandler</c> como argumento genérico de uma biblioteca de mock
/// (<c>Substitute.For&lt;HttpMessageHandler&gt;()</c>, <c>new Mock&lt;HttpMessageHandler&gt;()</c>).</item>
/// </list>
/// Usar o handler real que o TestServer cria (<c>server.CreateHandler()</c>, ADR-0012 regra 9) é permitido.
/// A regra só avalia o que o próprio assembly de teste usa: dependências de tipo e referências de assembly emitidas
/// pelo compilador. Um pacote que só chega por transitividade não conta. É o caso do InMemory do fixture de
/// violações, que flui para a saída do Juker.ArchitectureTests sem que o assembly o referencie.
/// </remarks>
public sealed class TestingPolicyRule : IArchitectureRule
{
    private const string HttpMessageHandler = "System.Net.Http.HttpMessageHandler";

    private static readonly string[] ForbiddenAssemblies =
    [
        "Microsoft.EntityFrameworkCore.InMemory",
        "Microsoft.EntityFrameworkCore.Sqlite",
        "Microsoft.Data.Sqlite",
        "RichardSzalay.MockHttp",
        "Moq.Contrib.HttpClient",
    ];

    private static readonly string[] MockingNamespaces = ["NSubstitute", "Moq", "FakeItEasy"];

    public string Id => "ARQ-08";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var byType = DependencyScan.Find(Id, subject.TestTypes, (_, target) =>
        {
            var assembly = TypeNames.AssemblyName(target);
            return ForbiddenAssemblies.Contains(assembly, StringComparer.Ordinal)
                ? $"usa {target.FullName} de {assembly}; IT usam o Postgres real e WireMock.Net (ADR-0012)"
                : null;
        });

        var mocked = subject.TestTypes
            .Where(type => type.Dependencies.Any(MocksHandler))
            .Select(type => new RuleViolation(
                Id,
                TypeNames.Offender(type),
                $"mock de {HttpMessageHandler} com biblioteca de mock; use o FakeHttpServer (WireMock.Net)"));

        // Handlers escritos à mão: herança por reflexão (a base fica num assembly que o ArchUnitNET não carrega).
        var handwritten = ArchitectureSubject.ReflectedTypes(subject.TestAssemblies)
            .Where(type => string.Equals(type.BaseType?.FullName, HttpMessageHandler, StringComparison.Ordinal))
            .Select(type => new RuleViolation(Id, type.FullName!, $"herda de {HttpMessageHandler} (mock escrito à mão); use o FakeHttpServer"));

        var byAssembly = subject.TestAssemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies()
                .Where(reference => ForbiddenAssemblies.Contains(reference.Name, StringComparer.Ordinal))
                .Select(reference => new RuleViolation(Id, assembly.GetName().Name!, $"referencia o assembly {reference.Name} (ADR-0012)")));

        return [.. byType.Concat(mocked).Concat(handwritten).Concat(byAssembly).Distinct()];
    }

    /// <summary>
    /// Dependência de uma biblioteca de mock com <c>HttpMessageHandler</c> como argumento genérico, do tipo
    /// (<c>new Mock&lt;HttpMessageHandler&gt;()</c>) ou do método (<c>Substitute.For&lt;HttpMessageHandler&gt;()</c>).
    /// Um <c>Func&lt;HttpMessageHandler&gt;</c> que devolve o handler do TestServer não passa por biblioteca de mock.
    /// </summary>
    private static bool MocksHandler(ITypeDependency dependency) =>
        MockingNamespaces.Any(ns => JukerNamespaces.IsWithin(TypeNames.Namespace(dependency.Target), ns))
        && (dependency.TargetGenericArguments.Any(argument => IsHandler(argument.Type))
            || (dependency is MethodCallDependency call && call.TargetMemberGenericArguments.Any(argument => IsHandler(argument.Type))));

    private static bool IsHandler(IType? type) => string.Equals(type?.FullName, HttpMessageHandler, StringComparison.Ordinal);
}
