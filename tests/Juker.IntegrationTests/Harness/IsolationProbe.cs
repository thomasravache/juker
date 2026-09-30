using AwesomeAssertions;
using Npgsql;

namespace Juker.IntegrationTests.Harness;

/// <summary>
/// Grava a mesma chave primária numa tabela de teste do banco da classe e confere que só enxerga o próprio dado.
/// A tabela não existe no juker_template: se duas classes dividissem o banco, o CREATE TABLE ou o INSERT falhariam.
/// </summary>
internal static class IsolationProbe
{
    public const int SharedKey = 1;

    public static async Task WriteAndReadOwnRowAsync(string connectionString, string owner, CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        await using (var create = dataSource.CreateCommand("CREATE TABLE isolation_probe (id integer PRIMARY KEY, owner text NOT NULL)"))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = dataSource.CreateCommand("INSERT INTO isolation_probe (id, owner) VALUES ($1, $2)"))
        {
            insert.Parameters.Add(new NpgsqlParameter { Value = SharedKey });
            insert.Parameters.Add(new NpgsqlParameter { Value = owner });
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        var rows = new List<(int Id, string Owner)>();
        await using (var select = dataSource.CreateCommand("SELECT id, owner FROM isolation_probe ORDER BY id"))
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((reader.GetInt32(0), reader.GetString(1)));
            }
        }

        rows.Should().Equal([(SharedKey, owner)], "cada classe enxerga só os próprios dados");
    }

    public static async Task ExtensionsShouldWorkAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        await using (var trigram = dataSource.CreateCommand("SELECT similarity('juker', 'jukebox') > 0"))
        {
            (await trigram.ExecuteScalarAsync(cancellationToken)).Should().Be(true, "pg_trgm deve estar criada no banco da classe");
        }

        await using (var unaccent = dataSource.CreateCommand("SELECT unaccent('Música no balcão')"))
        {
            (await unaccent.ExecuteScalarAsync(cancellationToken)).Should().Be("Musica no balcao", "unaccent deve estar criada no banco da classe");
        }
    }
}
