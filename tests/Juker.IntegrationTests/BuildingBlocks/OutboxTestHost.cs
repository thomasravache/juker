using Juker.Infrastructure;
using Juker.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TestModules.Outbox;

namespace Juker.IntegrationTests.BuildingBlocks;

/// <summary>
/// Host genérico com o kit montado como no Juker.Host (AddJukerInfrastructure + AddModuleDbContext + AddModuleHandlers)
/// sobre o módulo de teste <c>TestModules.Outbox</c>, no banco da classe (com a migration real do módulo aplicada).
/// Pode ser parado e religado sobre o mesmo banco (IT-02).
/// </summary>
internal sealed class OutboxTestHost : IAsyncDisposable
{
    private readonly IHost _host;

    private OutboxTestHost(IHost host) => _host = host;

    public IServiceProvider Services => _host.Services;

    public static async Task<OutboxTestHost> StartAsync(string connectionString, OutboxTestHostOptions options, CancellationToken ct)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = "Testing",
            ApplicationName = typeof(OutboxTestHost).Assembly.GetName().Name,
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:juker"] = connectionString,
            ["Outbox:DispatcherEnabled"] = options.DispatcherEnabled.ToString(),
            ["Outbox:IntervalMs"] = options.IntervalMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

        builder.Services.AddLogging();
        if (options.Time is { } time)
        {
            // Antes do kit: AddJukerInfrastructure usa TryAdd para o TimeProvider.System.
            builder.Services.AddSingleton(time);
        }

        builder.Services.AddSingleton(options.Probe);
        builder.Services.AddSingleton<ITenantContext>(new FixedTenantContext(options.Tenant));
        builder.Services.AddJukerInfrastructure(builder.Configuration);
        builder.Services.AddModuleDbContext<OutboxTestDbContext>();
        builder.Services.AddModuleHandlers(typeof(OutboxTestDbContext).Assembly);

        var host = builder.Build();
        var testHost = new OutboxTestHost(host);
        await using (var context = testHost.CreateContext(tenant: null))
        {
            await context.Database.MigrateAsync(ct);
        }

        await host.StartAsync(ct);
        return testHost;
    }

    /// <summary>Um contexto do módulo com as opções registradas pelo kit (Npgsql + interceptor) e o tenant informado.</summary>
    public OutboxTestDbContext CreateContext(VenueId? tenant)
    {
        using var scope = _host.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<OutboxTestDbContext>>();
        return new OutboxTestDbContext(options, new FixedTenantContext(tenant));
    }

    /// <summary>Grava, numa transação, um item que carrega os eventos informados (vão para o outbox pelo interceptor).</summary>
    public async Task PublishAsync(VenueId venue, IEnumerable<IIntegrationEvent> events, CancellationToken ct)
    {
        await using var context = CreateContext(venue);
        var carrier = TestItem.Carrier(Guid.NewGuid(), venue);
        foreach (var integrationEvent in events)
        {
            carrier.Publish(integrationEvent);
        }

        context.Items.Add(carrier);
        await context.SaveChangesAsync(ct);
    }

    public IReadOnlyList<string> HostedServiceNames() =>
        [.. _host.Services.GetServices<IHostedService>().Select(service => service.GetType().Name)];

    public Task StopAsync(CancellationToken ct) => _host.StopAsync(ct);

    public async ValueTask DisposeAsync()
    {
        if (_host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _host.Dispose();
        }
    }
}
