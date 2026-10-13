using Xunit;

namespace Juker.IntegrationTests.Harness;

/// <summary>
/// Coleção dos testes que rodam um <c>dotnet test --solution</c> aninhado sobre o build atual (SPEC-0027, item 4):
/// o contrato do TestKit (SPEC-0003:CT-01) e os comandos filtrados (SPEC-0027:IT-03).
/// Com a paralelização desligada, o xUnit executa a coleção sozinha, depois de todas as outras: a listagem aninhada
/// fica isolada dos builds aninhados dos fixtures (SPEC-0002:IT-03, SPEC-0026:IT-01, SPEC-0003:IT-05,
/// SPEC-0027:IT-01) e da carga do restante da suíte, que roda em paralelo. A causa da falha intermitente do CT-01
/// não foi confirmada; a provável é a disputa de <c>bin</c>/<c>obj</c> com esses builds, embora eles usem pastas
/// próprias ou um <c>--artifacts-path</c> temporário.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NestedTestRun
{
    public const string Name = "Execução aninhada de dotnet test";
}
