namespace SyncOverAsyncViolation.Configured;

public static class ConfiguredValueTaskViolation
{
    public static void Bloqueia() => ValueTask.CompletedTask.ConfigureAwait(false).GetAwaiter().GetResult();
}
