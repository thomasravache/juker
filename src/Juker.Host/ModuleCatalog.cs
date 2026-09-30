using Juker.Modules.Catalog;
using Juker.Modules.Playback;
using Juker.Modules.Queue;
using Juker.Modules.Sessions;
using Juker.Modules.Venues;

namespace Juker.Host;

/// <summary>Os módulos compostos pelo host, na ordem de registro (contrato da SPEC-0002).</summary>
public static class ModuleCatalog
{
    public static IReadOnlyList<ModuleDescriptor> All { get; } =
    [
        new("Venues", VenuesModule.AddVenuesModule, VenuesModule.MapVenuesEndpoints),
        new("Sessions", SessionsModule.AddSessionsModule, SessionsModule.MapSessionsEndpoints),
        new("Catalog", CatalogModule.AddCatalogModule, CatalogModule.MapCatalogEndpoints),
        new("Queue", QueueModule.AddQueueModule, QueueModule.MapQueueEndpoints),
        new("Playback", PlaybackModule.AddPlaybackModule, PlaybackModule.MapPlaybackEndpoints),
    ];
}
