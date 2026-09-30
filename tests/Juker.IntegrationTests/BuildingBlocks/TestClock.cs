using Microsoft.Extensions.Time.Testing;

namespace Juker.IntegrationTests.BuildingBlocks;

/// <summary>Relógio controlado dos IT do outbox, no mesmo instante inicial da <c>JukerApiFactory</c>.</summary>
internal static class TestClock
{
    public static readonly TimeSpan DispatcherInterval = TimeSpan.FromMilliseconds(500);

    public static FakeTimeProvider New() => new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
}
