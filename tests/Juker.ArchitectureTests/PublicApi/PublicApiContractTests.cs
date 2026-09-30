using System.Reflection;
using AwesomeAssertions;
using PublicApiGenerator;
using Xunit;

namespace Juker.ArchitectureTests.PublicApi;

/// <summary>
/// CT-01 da SPEC-0004: a API pública do kit (Juker.SharedKernel e Juker.Infrastructure) é contrato. O snapshot
/// aprovado foi escrito a partir da seção 6 da spec; qualquer diferença exige emenda.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PublicApiContractTests
{
    [Fact]
    [Trait("Spec", "SPEC-0004:CT-01")]
    public void Api_publica_do_SharedKernel_bate_com_o_snapshot_aprovado() =>
        Render(Load("Juker.SharedKernel")).Should().Be(Approved("Juker.SharedKernel"));

    [Fact]
    [Trait("Spec", "SPEC-0004:CT-01")]
    public void Api_publica_do_Infrastructure_bate_com_o_snapshot_aprovado() =>
        Render(Load("Juker.Infrastructure")).Should().Be(Approved("Juker.Infrastructure"));

    // Por nome: o fixture de violações (ARQ) também declara tipos no namespace Juker.SharedKernel.
    private static Assembly Load(string assemblyName) => Assembly.Load(new AssemblyName(assemblyName));

    private static string Render(Assembly assembly) =>
        Normalize(assembly.GeneratePublicApi(new ApiGeneratorOptions { IncludeAssemblyAttributes = false }));

    private static string Approved(string assemblyName) =>
        Normalize(File.ReadAllText(RepositoryPaths.Combine("tests", "Juker.ArchitectureTests", "PublicApi", $"{assemblyName}.approved.txt")));

    // Só normaliza fim de linha e a quebra final do arquivo; o resto do texto é comparado exatamente.
    private static string Normalize(string text) => text.ReplaceLineEndings("\n").TrimEnd('\n') + "\n";
}
