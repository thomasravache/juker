using AwesomeAssertions;
using Juker.IntegrationTests.Tooling;
using Xunit;

namespace Juker.IntegrationTests.Harness;

[Trait("Category", "Integration")]
public sealed class TemplateDatabaseMigrationTests
{
    private const string FixtureAssembly = "Juker.IntegrationTests.CleanMigration";

    // Tabela criada pela única migration do fixture (CreateGadgets), no schema do GadgetsDbContext.
    private const string MigratedTable = "clean_migration.gadgets";

    [Fact(Timeout = 600_000)]
    [Trait("Spec", "SPEC-0027:IT-01")]
    public async Task Banco_clonado_da_classe_tem_a_tabela_da_migration_de_um_DbContext_aplicada_pelo_modelo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspace = Directory.CreateTempSubdirectory("juker-0027-it01-");
        try
        {
            // Dado um DbContext de fixture com uma migration, num processo próprio (o juker_template desta suíte
            // continua só com os módulos reais, SPEC-0003:IT-04)
            var artifacts = Path.Combine(workspace.FullName, "artifacts");
            var build = await DotnetCli.RunAsync(
                ["build", RepositoryPaths.Combine("tests", "Juker.IntegrationTests", "Fixtures", "CleanMigration"), "-c", "Release", "--artifacts-path", artifacts],
                cancellationToken);
            build.ExitCode.Should().Be(0, "o fixture deve compilar. Saída:\n{0}", build.Output);
            var executable = Directory.EnumerateFiles(artifacts, FixtureAssembly + ".dll", SearchOption.AllDirectories)
                .Single(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

            // Quando a PostgresFixture prepara o juker_template (TemplateDatabase) e clona o banco de uma classe
            var tablesFile = Path.Combine(workspace.FullName, "tables.txt");
            var run = await DotnetCli.RunAsync([executable, tablesFile], cancellationToken);
            run.ExitCode.Should().Be(0, "o fixture deve preparar o modelo e clonar o banco. Saída:\n{0}", run.Output);

            // Então o banco clonado contém a tabela criada pela migration
            var tables = await File.ReadAllLinesAsync(tablesFile, cancellationToken);
            tables.Should().Contain(MigratedTable, "a migration do DbContext deve ser aplicada no juker_template e chegar ao clone");
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }
}
