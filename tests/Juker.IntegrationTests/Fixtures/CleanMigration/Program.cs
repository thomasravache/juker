using Juker.TestKit;
using Npgsql;

// SPEC-0027:IT-01 — prepara o juker_template com o GadgetsDbContext deste processo, clona o banco de uma "classe"
// e grava as tabelas desse banco (schema.tabela, uma por linha) no arquivo do primeiro argumento.
if (args is not [var output])
{
    throw new ArgumentException("Uso: Juker.IntegrationTests.CleanMigration <arquivo de saída>", nameof(args));
}

var fixture = new PostgresFixture();
await fixture.InitializeAsync();
try
{
    var connectionString = await fixture.CreateDatabaseAsync();

    var tables = new List<string>();
    await using (var dataSource = NpgsqlDataSource.Create(connectionString))
    await using (var command = dataSource.CreateCommand(
        """
        SELECT table_schema || '.' || table_name FROM information_schema.tables
        WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')
        ORDER BY 1
        """))
    await using (var reader = await command.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }
    }

    await File.WriteAllLinesAsync(output, tables);
}
finally
{
    await fixture.DisposeAsync();
}
