namespace Juker.ArchitectureTests.Rules;

/// <summary>ARQ-08 — testes sem EF Core InMemory/Sqlite nem mocks de <c>HttpMessageHandler</c> (ADR-0012).</summary>
/// <remarks>
/// Avalia só o que o próprio assembly de teste usa: dependências de tipo e referências de assembly emitidas pelo
/// compilador. Um pacote que só chega por transitividade (por exemplo, o InMemory do fixture de violações, que flui
/// para a saída do Juker.ArchitectureTests) não conta, porque o assembly não o referencia.
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

    public string Id => "ARQ-08";

    public IReadOnlyList<RuleViolation> Evaluate(ArchitectureSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        // Uso de tipos dos pacotes proibidos ou do HttpMessageHandler (Substitute.For<HttpMessageHandler>(), Mock<…>).
        var byType = DependencyScan.Find(Id, subject.TestTypes, (_, target) =>
        {
            var assembly = TypeNames.AssemblyName(target);
            if (ForbiddenAssemblies.Contains(assembly, StringComparer.Ordinal))
            {
                return $"usa {target.FullName} de {assembly}; IT usam o Postgres real e WireMock.Net (ADR-0012)";
            }

            return string.Equals(target.FullName, HttpMessageHandler, StringComparison.Ordinal)
                ? $"usa {HttpMessageHandler} diretamente (mock de HttpMessageHandler); use o FakeHttpServer (WireMock.Net)"
                : null;
        });

        // Handlers escritos à mão: herança por reflexão (a base fica num assembly que o ArchUnitNET não carrega).
        var handlers = ArchitectureSubject.ReflectedTypes(subject.TestAssemblies)
            .Where(type => string.Equals(type.BaseType?.FullName, HttpMessageHandler, StringComparison.Ordinal))
            .Select(type => new RuleViolation(Id, type.FullName!, $"herda de {HttpMessageHandler} (mock de HttpMessageHandler); use o FakeHttpServer"));

        var byAssembly = subject.TestAssemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies()
                .Where(reference => ForbiddenAssemblies.Contains(reference.Name, StringComparer.Ordinal))
                .Select(reference => new RuleViolation(Id, assembly.GetName().Name!, $"referencia o assembly {reference.Name} (ADR-0012)")));

        return [.. byType.Concat(handlers).Concat(byAssembly).Distinct()];
    }
}
