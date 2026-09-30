using Xunit;

#pragma warning disable MA0025 // Scaffold do contrato (SPEC-0003): corpos lançam NotImplementedException até a implementação

namespace Juker.TestKit;

/// <summary>
/// Um container <c>postgres:18</c> por execução (fixture de assembly). Na subida, aplica as migrations reais de
/// todos os módulos no banco <c>juker_template</c>; cada classe de teste recebe um banco clonado dele (ADR-0012).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Cria um banco novo para a classe, clonado do <c>juker_template</c> (&lt; 1 s), e devolve a connection string.</summary>
    public Task<string> CreateDatabaseAsync() => throw new NotImplementedException();

    // Scaffold (SPEC-0003): sem container ainda; a subida real entra na implementação.
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
