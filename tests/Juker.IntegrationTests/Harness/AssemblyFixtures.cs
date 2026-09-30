using Juker.TestKit;
using Xunit;

// Um container Postgres por execução do assembly (ADR-0012, regra 1).
[assembly: AssemblyFixture(typeof(PostgresFixture))]
