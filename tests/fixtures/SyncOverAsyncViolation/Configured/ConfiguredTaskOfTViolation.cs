namespace SyncOverAsyncViolation.Configured;

public static class ConfiguredTaskOfTViolation
{
    public static int Bloqueia() => Task.FromResult(42).ConfigureAwait(false).GetAwaiter().GetResult();
}
