namespace SyncOverAsyncViolation.Configured;

public static class ConfiguredTaskViolation
{
    public static void Bloqueia() => Task.CompletedTask.ConfigureAwait(false).GetAwaiter().GetResult();
}
