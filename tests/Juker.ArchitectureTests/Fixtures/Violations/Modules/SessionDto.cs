using Juker.Modules.Sessions.Domain;

namespace Juker.Modules.Sessions.Contracts;

/// <summary>ARQ-02: um tipo do .Contracts expõe um tipo interno do módulo.</summary>
public sealed record SessionDto(Session Session);
