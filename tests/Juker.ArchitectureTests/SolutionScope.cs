using AwesomeAssertions;
using Juker.ArchitectureTests.Rules;

namespace Juker.ArchitectureTests;

/// <summary>Garante que a avaliação "solução real" não é vazia nem inclui os fixtures.</summary>
internal static class SolutionScope
{
    private static readonly string[] ExpectedProduction =
    [
        "Juker.Host",
        "Juker.SharedKernel",
        "Juker.Modules.Venues",
        "Juker.Modules.Venues.Contracts",
        "Juker.Modules.Sessions",
        "Juker.Modules.Sessions.Contracts",
        "Juker.Modules.Catalog",
        "Juker.Modules.Catalog.Contracts",
        "Juker.Modules.Queue",
        "Juker.Modules.Queue.Contracts",
        "Juker.Modules.Playback",
        "Juker.Modules.Playback.Contracts",
        "Juker.Web",
        "Juker.Web.Client",
    ];

    private static readonly string[] ExpectedTests = ["Juker.ArchitectureTests", "Juker.IntegrationTests"];

    private static readonly string[] Fixtures = ["Juker.ArchitectureTests.Violations", "Juker.ArchitectureTests.ConventionViolations"];

    public static void ShouldCoverTheSolution(ArchitectureSubject subject)
    {
        var production = subject.ProductionAssemblies.Select(assembly => assembly.GetName().Name).ToList();
        var tests = subject.TestAssemblies.Select(assembly => assembly.GetName().Name).ToList();

        production.Should().Contain(ExpectedProduction).And.NotContain(Fixtures);
        tests.Should().Contain(ExpectedTests).And.NotContain(Fixtures);
    }
}
