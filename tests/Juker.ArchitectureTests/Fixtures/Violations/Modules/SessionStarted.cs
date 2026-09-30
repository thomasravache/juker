using Juker.Modules.Queue.Contracts;
using Juker.SharedKernel;

namespace Juker.Modules.Sessions.Contracts;

/// <summary>Controle negativo do ARQ-02: .Contracts pode referenciar o SharedKernel e outros .Contracts.</summary>
public sealed record SessionStarted(Guid SessionId, QueueSnapshot Queue, Result Outcome);
