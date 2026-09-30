using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Juker.Host;
using Juker.Host.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Juker.IntegrationTests.Tooling;

[Trait("Category", "Integration")]
public sealed class DiagnosticsModulesTests
{
    // /_diag/modules não consulta o banco: basta uma connection string sintaticamente válida para o AddNpgsqlDataSource.
    private const string ConnectionStringKey = "ConnectionStrings:juker";
    private const string UnusedConnectionString = "Host=localhost;Database=juker;Username=juker;Password=juker";
    private static readonly Uri DiagModules = new("/_diag/modules", UriKind.Relative);

    [Fact]
    [Trait("Spec", "SPEC-0026:IT-02")]
    public async Task Diagnostico_lista_os_modulos_compostos_pelo_host_e_nao_o_catalogo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado um host de teste em Development cuja composição de módulos é substituída para registrar só Venues
        await using var root = new WebApplicationFactory<ModuleDescriptor>();
        await using var factory = CreateHost(root, "Development", services =>
        {
            services.RemoveAll<ComposedModules>();
            services.AddSingleton(new ComposedModules(["Venues"]));
        });
        using var client = factory.CreateClient();

        // Quando GET /_diag/modules é chamado
        using var response = await client.GetAsync(DiagModules, cancellationToken);

        // Então a resposta é ["Venues"] (os módulos compostos, não os cinco nomes do ModuleCatalog)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var modules = await response.Content.ReadFromJsonAsync<string[]>(cancellationToken);
        modules.Should().Equal("Venues");
    }

    [Fact]
    [Trait("Spec", "SPEC-0026:IT-03")]
    public async Task Diagnostico_responde_404_em_Production()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dado o host em Production
        await using var root = new WebApplicationFactory<ModuleDescriptor>();
        await using var factory = CreateHost(root, "Production", _ => { });
        using var client = factory.CreateClient();

        // Quando GET /_diag/modules é chamado
        using var response = await client.GetAsync(DiagModules, cancellationToken);

        // Então a resposta é 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // O tipo genérico só identifica o assembly do host (Juker.Host): "Program" é ambíguo com o do AppHost neste projeto.
    private static WebApplicationFactory<ModuleDescriptor> CreateHost(
        WebApplicationFactory<ModuleDescriptor> root,
        string environment,
        Action<IServiceCollection> configureServices)
    {
        return root.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting(ConnectionStringKey, UnusedConnectionString);
            builder.ConfigureTestServices(configureServices);
        });
    }
}
