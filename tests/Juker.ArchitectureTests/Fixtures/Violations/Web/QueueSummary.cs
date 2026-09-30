using Juker.Modules.Queue.Contracts;
using Juker.SharedKernel;

namespace Juker.Web.Components;

/// <summary>Controle negativo do ARQ-05: o Web pode usar o .Contracts dos módulos e o SharedKernel.</summary>
public sealed class QueueSummary
{
    public QueueSnapshot? Snapshot { get; set; }

    public Result? LastResult { get; set; }
}
