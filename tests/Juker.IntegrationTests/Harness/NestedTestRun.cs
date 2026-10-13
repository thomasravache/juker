using Xunit;

namespace Juker.IntegrationTests.Harness;

/// <summary>
/// Coleção dos testes que rodam um <c>dotnet test --solution</c> aninhado sobre o build atual (SPEC-0027, item 4).
/// Com a paralelização desligada, o xUnit executa a coleção sozinha, depois de todas as outras: a listagem aninhada
/// lê os assemblies de teste da solução sem disputar <c>bin</c>/<c>obj</c> com os builds aninhados dos fixtures
/// (SPEC-0002:IT-03, SPEC-0026:IT-01, SPEC-0003:IT-05, SPEC-0027:IT-01), que rodam em paralelo no restante da suíte.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NestedTestRun
{
    public const string Name = "Execução aninhada de dotnet test";
}
