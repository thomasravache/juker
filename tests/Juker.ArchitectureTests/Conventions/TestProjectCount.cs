namespace Juker.ArchitectureTests.Conventions;

/// <summary>Um projeto de teste, se ele aceita zero testes e quantos métodos de teste declara.</summary>
/// <param name="Project">Caminho do projeto, relativo à pasta da solução.</param>
/// <param name="AllowsZeroTests">Se o projeto declara <c>JukerAllowZeroTests=true</c>.</param>
/// <param name="TestCount">Quantidade de métodos <c>[Fact]</c>/<c>[Theory]</c> no assembly do projeto.</param>
public sealed record TestProjectCount(string Project, bool AllowsZeroTests, int TestCount);
