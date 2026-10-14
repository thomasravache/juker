using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Juker.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juker.IntegrationTests.Testing;

[Trait("Category", "Integration")]
public sealed class TestEnvironmentTests(PostgresFixture db)
{
    private const string E2EEnvironment = "E2E";
    private const string ClockAdvancePath = "/_test/clock/advance";
    private const string PingPath = "/_test/ping";
    private const string VenuesPath = "/_test/venues";

    private static readonly TimeSpan FifteenMinutes = TimeSpan.FromMinutes(15);

    // O relógio controlável pode seguir o tempo real entre as duas leituras; a folga cobre só a duração do teste.
    private static readonly TimeSpan RealTimeSlack = TimeSpan.FromMinutes(1);

    [Fact]
    [Trait("Spec", "SPEC-0025:IT-02")]
    public async Task Avancar_o_relogio_pela_API_de_teste_adianta_o_TimeProvider_do_host_em_15_minutos()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado o host real no ambiente E2E, com o relógio controlável no lugar do TimeProvider do sistema
        await using var factory = new HostEnvironmentFactory(await db.CreateDatabaseAsync(), E2EEnvironment);
        using var client = factory.CreateClient();
        var time = factory.Services.GetRequiredService<TimeProvider>();
        time.Should().NotBeSameAs(TimeProvider.System, "no ambiente E2E o TimeProvider é o ControllableTimeProvider");
        var before = time.GetUtcNow();

        // Quando o teste pede para avançar 900 s
        using var response = await client.PostAsJsonAsync(new Uri(ClockAdvancePath, UriKind.Relative), new { seconds = 900 }, cancellationToken);

        // Então a API responde 204 e o TimeProvider do host fica 15 min à frente
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var advanced = time.GetUtcNow() - before;
        advanced.Should().BeGreaterThanOrEqualTo(FifteenMinutes).And.BeLessThan(FifteenMinutes + RealTimeSlack);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    [Trait("Spec", "SPEC-0025:IT-02")]
    public async Task Fora_do_ambiente_E2E_a_API_de_teste_responde_404_e_o_relogio_e_o_do_sistema(string environment)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado que a página de teste existe no host em E2E (a mesma rota, para a comparação não ser vazia)
        await using (var e2e = new HostEnvironmentFactory(await db.CreateDatabaseAsync(), E2EEnvironment))
        {
            using var e2eClient = e2e.CreateClient();
            using var ping = await e2eClient.GetAsync(new Uri(PingPath, UriKind.Relative), cancellationToken);
            ping.StatusCode.Should().Be(HttpStatusCode.OK, "/_test/ping existe no ambiente E2E");
        }

        // Quando o host sobe em Staging ou Production
        await using var factory = new HostEnvironmentFactory(await db.CreateDatabaseAsync(), environment);
        using var client = factory.CreateClient();

        // Então /_test/* responde 404
        using (var ping = await client.GetAsync(new Uri(PingPath, UriKind.Relative), cancellationToken))
        {
            ping.StatusCode.Should().Be(HttpStatusCode.NotFound, "GET {0} não existe em {1}", PingPath, environment);
        }

        using (var advance = await client.PostAsJsonAsync(new Uri(ClockAdvancePath, UriKind.Relative), new { seconds = 900 }, cancellationToken))
        {
            advance.StatusCode.Should().Be(HttpStatusCode.NotFound, "POST {0} não existe em {1}", ClockAdvancePath, environment);
        }

        using (var venues = await client.PostAsJsonAsync(new Uri(VenuesPath, UriKind.Relative), new { slug = "e2e-404" }, cancellationToken))
        {
            venues.StatusCode.Should().Be(HttpStatusCode.NotFound, "POST {0} não existe em {1}", VenuesPath, environment);
        }

        // E o TimeProvider resolvido é o do sistema
        factory.Services.GetRequiredService<TimeProvider>().Should().BeSameAs(TimeProvider.System);
    }
}
