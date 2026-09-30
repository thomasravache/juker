using Juker.Modules.Queue.Domain;

namespace Juker.Web.Components;

/// <summary>ARQ-05: o Web usa um tipo interno do módulo Queue (e não o .Contracts).</summary>
public sealed class QueuePanel
{
    public QueueItem? Current { get; set; }
}
