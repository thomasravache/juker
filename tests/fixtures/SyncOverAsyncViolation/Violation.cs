namespace SyncOverAsyncViolation;

public static class Violation
{
    public static void Bloqueia() => Task.CompletedTask.GetAwaiter().GetResult();
}
