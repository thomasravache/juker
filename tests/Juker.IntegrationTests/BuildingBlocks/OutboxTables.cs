using Npgsql;
using TestModules.Outbox;

namespace Juker.IntegrationTests.BuildingBlocks;

/// <summary>Lê o outbox e o inbox do módulo de teste direto no Postgres, pelos nomes de tabela e coluna do contrato.</summary>
internal static class OutboxTables
{
    private const string Schema = OutboxTestDbContext.SchemaName;

    public static async Task<IReadOnlyList<OutboxRow>> OutboxAsync(string connectionString, CancellationToken ct)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(
            $"SELECT id, type, payload::text, occurred_at, status, attempts, next_attempt_at, processed_at, last_error " +
            $"FROM {Schema}.outbox_messages ORDER BY occurred_at, id");
        await using var reader = await command.ExecuteReaderAsync(ct);

        var rows = new List<OutboxRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new OutboxRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                await reader.IsDBNullAsync(7, ct) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                await reader.IsDBNullAsync(8, ct) ? null : reader.GetString(8)));
        }

        return rows;
    }

    public static async Task<OutboxRow> OutboxMessageAsync(string connectionString, Guid id, CancellationToken ct) =>
        (await OutboxAsync(connectionString, ct)).Single(row => row.Id == id);

    public static async Task<IReadOnlyList<InboxRow>> InboxAsync(string connectionString, CancellationToken ct)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(
            $"SELECT message_id, consumer, processed_at FROM {Schema}.inbox_messages ORDER BY message_id, consumer");
        await using var reader = await command.ExecuteReaderAsync(ct);

        var rows = new List<InboxRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new InboxRow(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2)));
        }

        return rows;
    }
}
