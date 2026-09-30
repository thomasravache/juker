using System.Diagnostics;
using System.Security.Cryptography;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Juker.TestKit;

/// <summary>
/// Um container <c>postgres:18</c> por execução (fixture de assembly). Na subida, aplica as migrations reais de
/// todos os módulos no banco <c>juker_template</c>; cada classe de teste recebe um banco clonado dele (ADR-0012).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string Image = "postgres:18";

    // O CREATE DATABASE … TEMPLATE exige que ninguém esteja conectado ao modelo: os clones são criados em série.
    private readonly SemaphoreSlim _cloneGate = new(1, 1);

    // Senha gerada por execução (SPEC-0003, seção 5): nada de segredo versionado.
    // Durabilidade desligada: o container é descartável e o clone do modelo fica bem abaixo de 1 s.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
        .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)))
        .WithCommand("-c", "fsync=off", "-c", "synchronous_commit=off", "-c", "full_page_writes=off")
        .Build();

    /// <summary>Cria um banco novo para a classe, clonado do <c>juker_template</c> (&lt; 1 s), e devolve a connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = TemplateDatabase.NewDatabaseName();
        await _cloneGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var connection = new NpgsqlConnection(TemplateDatabase.Maintenance(_container.GetConnectionString()));
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = TemplateDatabase.CloneCommand(connection, name);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        finally
        {
            _cloneGate.Release();
        }

        return TemplateDatabase.ForDatabase(_container.GetConnectionString(), name);
    }

    public async ValueTask InitializeAsync()
    {
        var start = Stopwatch.GetTimestamp();
        await _container.StartAsync().ConfigureAwait(false);
        var containerReady = Stopwatch.GetElapsedTime(start);
        await TemplateDatabase.CreateAsync(_container.GetConnectionString()).ConfigureAwait(false);
        var templateReady = Stopwatch.GetElapsedTime(start);

        // Evidência do requisito de subida < 10 s com a imagem em cache (SPEC-0003, seção 5), na saída do dotnet test.
        TestContext.Current.SendDiagnosticMessage(
            "PostgresFixture: container {0} pronto em {1:F0} ms; {2} (migrations e extensões) pronto em {3:F0} ms.",
            Image,
            containerReady.TotalMilliseconds,
            TemplateDatabase.Name,
            templateReady.TotalMilliseconds);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
        _cloneGate.Dispose();
    }

    /// <summary>
    /// Versão síncrona de <see cref="CreateDatabaseAsync"/> para quem não pode aguardar, como a
    /// <see cref="JukerApiFactory"/> ao construir o host (API síncrona do Npgsql, sem bloquear uma Task).
    /// </summary>
    internal string CreateDatabase()
    {
        var name = TemplateDatabase.NewDatabaseName();
        _cloneGate.Wait();
        try
        {
            using var connection = new NpgsqlConnection(TemplateDatabase.Maintenance(_container.GetConnectionString()));
            connection.Open();
            using var command = TemplateDatabase.CloneCommand(connection, name);
            command.ExecuteNonQuery();
        }
        finally
        {
            _cloneGate.Release();
        }

        return TemplateDatabase.ForDatabase(_container.GetConnectionString(), name);
    }
}
