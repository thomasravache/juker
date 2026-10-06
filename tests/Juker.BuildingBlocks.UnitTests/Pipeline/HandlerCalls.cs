using System.Collections.Concurrent;

namespace Juker.BuildingBlocks.UnitTests.Pipeline;

/// <summary>Registra as chamadas ao handler de teste (singleton do DI do teste).</summary>
public sealed class HandlerCalls
{
    private readonly ConcurrentQueue<RenameThing> _calls = new();

    public IReadOnlyList<RenameThing> Calls => [.. _calls];

    public void Record(RenameThing command) => _calls.Enqueue(command);
}
