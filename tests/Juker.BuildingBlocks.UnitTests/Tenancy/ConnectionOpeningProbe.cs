using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Juker.BuildingBlocks.UnitTests.Tenancy;

/// <summary>Conta as tentativas de abrir conexão: o fail closed tem que acontecer antes de qualquer I/O.</summary>
public sealed class ConnectionOpeningProbe : DbConnectionInterceptor
{
    private int _openings;

    public int Openings => Volatile.Read(ref _openings);

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        Interlocked.Increment(ref _openings);
        return base.ConnectionOpening(connection, eventData, result);
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _openings);
        return base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
    }
}
