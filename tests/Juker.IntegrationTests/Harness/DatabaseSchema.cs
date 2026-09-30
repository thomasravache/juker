namespace Juker.IntegrationTests.Harness;

/// <summary>O que o IT-04 lê de um banco clonado: tabelas, extensões e migrations aplicadas.</summary>
internal sealed record DatabaseSchema(
    IReadOnlyList<string> Tables,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<string> AppliedMigrations);
