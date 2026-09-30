using Microsoft.EntityFrameworkCore;

namespace Juker.Modules.Queue.UnitTests;

/// <summary>ARQ-08: um "projeto de teste" que usa o EF Core InMemory em vez do Postgres real.</summary>
public static class InMemoryQueueTests
{
    public static DbContextOptions CreateOptions() =>
        new DbContextOptionsBuilder().UseInMemoryDatabase("queue").Options;
}
