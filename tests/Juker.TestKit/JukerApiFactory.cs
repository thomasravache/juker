using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Juker.TestKit;

/// <summary>
/// Host real do Juker para IT (ADR-0012): banco da classe, <see cref="FakeTimeProvider"/> no lugar do
/// <see cref="TimeProvider"/>, <c>Outbox:DispatcherEnabled=false</c> e os esquemas de auth reais.
/// </summary>
public sealed class JukerApiFactory(PostgresFixture db) : WebApplicationFactory<Program>
{
    // Nomes do host (SPEC-0002): connection string do AppHost e ambiente com as rotas de diagnóstico.
    private const string ConnectionStringKey = "ConnectionStrings:juker";
    private const string TestingEnvironment = "Testing";

    // Outbox (ADR-0006/ADR-0012, regra 6): nos IT o dispatcher fica desligado; o DrainOutboxAsync vem na SPEC-0004.
    private const string OutboxDispatcherKey = "Outbox:DispatcherEnabled";

    // Instante fixo e conhecido: o teste avança o relógio explicitamente (ADR-0012, regra 7).
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _db = db;
    private readonly List<Action<IServiceCollection>> _testIssuers = [];
    private bool _hostConfigured;

    /// <summary>Relógio controlado pelo teste; substitui o <see cref="TimeProvider"/> do host.</summary>
    public FakeTimeProvider Time { get; } = new(Epoch);

    /// <summary>Ponto de extensão para os emissores de credenciais de teste (SPEC-0009).</summary>
    /// <remarks>
    /// Os emissores registram serviços que produzem credenciais válidas para os esquemas reais (Staff, Guest, Tv);
    /// nunca um handler que ignore a autorização. Deve ser chamado antes do primeiro uso do host.
    /// </remarks>
    public void UseTestIssuers(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (_hostConfigured)
        {
            throw new InvalidOperationException("UseTestIssuers deve ser chamado antes de o host ser criado (CreateClient/Services).");
        }

        _testIssuers.Add(configure);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _hostConfigured = true;

        // Banco próprio desta factory, clonado do juker_template (síncrono: o host é construído de forma síncrona).
        builder.UseEnvironment(TestingEnvironment);
        builder.UseSetting(ConnectionStringKey, _db.CreateDatabase());
        builder.UseSetting(OutboxDispatcherKey, bool.FalseString);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            foreach (var configure in _testIssuers)
            {
                configure(services);
            }
        });
    }
}
