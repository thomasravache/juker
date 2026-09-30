using Juker.Modules.Queue.Domain;

namespace Juker.Modules.Venues.Features.CheckIn;

/// <summary>ARQ-01: o módulo Venues usa um tipo interno do módulo Queue (e não o .Contracts).</summary>
public static class CheckInHandler
{
    public static Guid Handle(QueueItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Id;
    }
}
