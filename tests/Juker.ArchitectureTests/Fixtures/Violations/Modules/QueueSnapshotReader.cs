using Juker.Modules.Queue.Contracts;

namespace Juker.Modules.Venues.Features.CheckIn;

/// <summary>Controle negativo do ARQ-01: usar o .Contracts de outro módulo é permitido.</summary>
public static class QueueSnapshotReader
{
    public static int Read(QueueSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Length;
    }
}
