namespace Juker.Infrastructure.Common;

internal sealed class IdGenerator(TimeProvider timeProvider) : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7(timeProvider.GetUtcNow());
}
