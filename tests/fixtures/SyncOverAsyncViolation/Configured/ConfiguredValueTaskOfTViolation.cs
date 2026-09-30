namespace SyncOverAsyncViolation.Configured;

public static class ConfiguredValueTaskOfTViolation
{
    public static int Bloqueia() => new ValueTask<int>(42).ConfigureAwait(false).GetAwaiter().GetResult();
}
