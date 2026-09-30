using System.Net;
using AwesomeAssertions;
using Juker.TestKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Juker.IntegrationTests.Harness;

[Trait("Category", "Integration")]
public sealed class JukerApiFactoryTests(PostgresFixture db)
{
    [Fact]
    [Trait("Spec", "SPEC-0003:IT-03")]
    public async Task Factory_sobe_o_host_real_com_banco_da_classe_FakeTimeProvider_e_dispatcher_desligado()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado a JukerApiFactory com o banco da classe e o relógio controlado que ela expõe
        await using var factory = new JukerApiFactory(db);
        var time = factory.Time;
        time.Should().NotBeNull();

        // Quando o host real sobe, então /health/ready responde 200 (Postgres do banco da classe)
        using var client = factory.CreateClient();
        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), cancellationToken);
        ready.StatusCode.Should().Be(HttpStatusCode.OK);

        // E o host usa o banco da classe (clonado do modelo), não o juker_template nem outro banco
        var database = new NpgsqlConnectionStringBuilder(factory.Services.GetRequiredService<NpgsqlDataSource>().ConnectionString).Database;
        database.Should().StartWith("juker_").And.NotBe("juker_template");

        // E o TimeProvider resolvido é o FakeTimeProvider exposto pela factory
        factory.Services.GetRequiredService<TimeProvider>().Should().BeSameAs(time);

        // E Outbox:DispatcherEnabled está configurado como falso
        var dispatcherEnabled = factory.Services.GetRequiredService<IConfiguration>()["Outbox:DispatcherEnabled"];
        bool.TryParse(dispatcherEnabled, out var enabled).Should().BeTrue("Outbox:DispatcherEnabled deve estar definido");
        enabled.Should().BeFalse();
    }
}
