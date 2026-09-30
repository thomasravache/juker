using Juker.Infrastructure;

namespace Juker.Modules.Sessions.Contracts;

/// <summary>ARQ-02: um tipo do .Contracts depende do Juker.Infrastructure (só SharedKernel e .Contracts são permitidos).</summary>
public sealed record SessionClosedEvent(OutboxMessage Message);
