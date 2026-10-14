using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juker.IntegrationTests.Testing;

/// <summary>
/// Servidor HTTP local (Kestrel numa porta livre) que faz o papel do ambiente implantado no SPEC-0025:CT-01: responde
/// <c>/health/ready</c> com 200, qualquer outro caminho com 404, e registra todos os caminhos pedidos.
/// </summary>
internal sealed class RecordingServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<string> _paths;

    private RecordingServer(WebApplication app, ConcurrentQueue<string> paths, Uri baseUrl)
    {
        _app = app;
        _paths = paths;
        BaseUrl = baseUrl;
    }

    public Uri BaseUrl { get; }

    public IReadOnlyList<string> Paths => [.. _paths];

    public static async Task<RecordingServer> StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        var paths = new ConcurrentQueue<string>();
        app.Use((context, next) =>
        {
            paths.Enqueue(context.Request.Path.Value ?? string.Empty);
            return next(context);
        });
        app.MapGet("/health/ready", () => Results.Text("Healthy"));

        await app.StartAsync(cancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new RecordingServer(app, paths, new Uri(address));
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
