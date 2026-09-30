using System.Diagnostics;
using AwesomeAssertions;
using Juker.TestKit;
using Npgsql;
using Xunit;

namespace Juker.IntegrationTests.Harness;

[Trait("Category", "Integration")]
public sealed class TemplateDatabaseTests(PostgresFixture db)
{
    private const string TemplateDatabase = "juker_template";
    private static readonly TimeSpan CloneBudget = TimeSpan.FromSeconds(1);
    private static readonly string[] TemplateExtensions = ["pg_trgm", "unaccent"];

    [Fact]
    [Trait("Spec", "SPEC-0003:IT-04")]
    public async Task CreateDatabaseAsync_clona_bancos_distintos_do_modelo_com_as_migrations_reais_em_menos_de_1s()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Quando duas classes pedem um banco à PostgresFixture
        var (first, firstElapsed) = await CreateTimedAsync();
        var (second, secondElapsed) = await CreateTimedAsync();

        // Então cada banco é criado em menos de 1 s
        firstElapsed.Should().BeLessThan(CloneBudget);
        secondElapsed.Should().BeLessThan(CloneBudget);

        // E os bancos são distintos entre si e do modelo
        var firstName = new NpgsqlConnectionStringBuilder(first).Database;
        var secondName = new NpgsqlConnectionStringBuilder(second).Database;
        firstName.Should().NotBeNullOrWhiteSpace();
        firstName.Should().NotBe(secondName).And.NotBe(TemplateDatabase);
        secondName.Should().NotBe(TemplateDatabase);

        // E ambos vêm do juker_template: mesmas tabelas, as migrations reais aplicadas e as extensões do modelo
        var expectedMigrations = RealMigrations.Ids();
        var firstSchema = await ReadSchemaAsync(first, cancellationToken);
        var secondSchema = await ReadSchemaAsync(second, cancellationToken);

        secondSchema.Tables.Should().Equal(firstSchema.Tables);
        firstSchema.AppliedMigrations.Should().Equal(expectedMigrations);
        secondSchema.AppliedMigrations.Should().Equal(expectedMigrations);
        firstSchema.Extensions.Should().Contain(TemplateExtensions);
        secondSchema.Extensions.Should().Contain(TemplateExtensions);
    }

    private async Task<(string ConnectionString, TimeSpan Elapsed)> CreateTimedAsync()
    {
        var start = Stopwatch.GetTimestamp();
        var connectionString = await db.CreateDatabaseAsync();
        return (connectionString, Stopwatch.GetElapsedTime(start));
    }

    private static async Task<DatabaseSchema> ReadSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        var tables = await ReadStringsAsync(
            dataSource,
            """
            SELECT table_schema || '.' || table_name FROM information_schema.tables
            WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY 1
            """,
            cancellationToken);

        var extensions = await ReadStringsAsync(dataSource, "SELECT extname FROM pg_extension ORDER BY 1", cancellationToken);

        var migrations = new List<string>();
        var historySchemas = await ReadStringsAsync(
            dataSource,
            "SELECT table_schema FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory' ORDER BY 1",
            cancellationToken);
        foreach (var schema in historySchemas)
        {
            var quoted = "\"" + schema.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
            migrations.AddRange(await ReadStringsAsync(
                dataSource,
                $"SELECT \"MigrationId\" FROM {quoted}.\"__EFMigrationsHistory\"",
                cancellationToken));
        }

        return new DatabaseSchema(tables, extensions, [.. migrations.Order(StringComparer.Ordinal)]);
    }

    private static async Task<List<string>> ReadStringsAsync(NpgsqlDataSource dataSource, string sql, CancellationToken cancellationToken)
    {
        var values = new List<string>();
        await using var command = dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
