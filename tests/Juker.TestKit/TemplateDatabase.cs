using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Juker.TestKit;

/// <summary>
/// O banco-modelo <c>juker_template</c> (ADR-0012, regra 2): extensões do produto e as migrations reais de todos os
/// <c>DbContext</c> da solução, aplicadas uma vez por execução; cada classe de teste recebe um clone.
/// </summary>
internal static class TemplateDatabase
{
    public const string Name = "juker_template";

    private const string MaintenanceDatabase = "postgres";

    // Extensões usadas pelos módulos (busca com pg_trgm e unaccent, ADR-0004), criadas no modelo antes das migrations.
    private static readonly string[] Extensions = ["pg_trgm", "unaccent"];

    public static async Task CreateAsync(string serverConnectionString)
    {
        await using (var maintenance = new NpgsqlConnection(Maintenance(serverConnectionString)))
        {
            await maintenance.OpenAsync().ConfigureAwait(false);
            await ExecuteAsync(maintenance, $"CREATE DATABASE {Name}").ConfigureAwait(false);
        }

        // Sem pool: nenhuma conexão ociosa pode ficar presa ao modelo, senão o CREATE DATABASE … TEMPLATE falha.
        var template = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = Name, Pooling = false }.ConnectionString;
        await using (var connection = new NpgsqlConnection(template))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            foreach (var extension in Extensions)
            {
                await ExecuteAsync(connection, $"CREATE EXTENSION IF NOT EXISTS {extension}").ConfigureAwait(false);
            }
        }

        foreach (var contextType in DbContextDiscovery.ContextTypes())
        {
            await using var context = DbContextDiscovery.Create(contextType);
            context.Database.SetConnectionString(template);
            await context.Database.MigrateAsync().ConfigureAwait(false);
        }

        await using (var maintenance = new NpgsqlConnection(Maintenance(serverConnectionString)))
        {
            await maintenance.OpenAsync().ConfigureAwait(false);
            await ExecuteAsync(maintenance, $"ALTER DATABASE {Name} WITH IS_TEMPLATE true ALLOW_CONNECTIONS false").ConfigureAwait(false);
        }
    }

    public static string NewDatabaseName() => $"juker_{Guid.NewGuid():N}";

    public static string Maintenance(string serverConnectionString) =>
        new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = MaintenanceDatabase, Pooling = false }.ConnectionString;

    public static string ForDatabase(string serverConnectionString, string database) =>
        new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = database }.ConnectionString;

    /// <summary>O nome vem de <see cref="NewDatabaseName"/> (letras, dígitos e _), nunca de entrada externa.</summary>
    public static NpgsqlCommand CloneCommand(NpgsqlConnection connection, string database) =>
        new($"CREATE DATABASE \"{database}\" TEMPLATE {Name}", connection);

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
