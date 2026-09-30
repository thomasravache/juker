using AwesomeAssertions;
using Juker.Host;
using Juker.Host.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juker.IntegrationTests.Tooling;

[Trait("Category", "Integration")]
public sealed class ComposedModulesRegistrationTests
{
    // O host real não consulta o banco para compor os módulos: basta uma connection string sintaticamente válida.
    private const string ConnectionStringKey = "ConnectionStrings:juker";
    private const string UnusedConnectionString = "Host=localhost;Database=juker;Username=juker;Password=juker";

    [Fact]
    [Trait("Spec", "SPEC-0028:IT-02")]
    public async Task Host_real_registra_ComposedModules_com_os_modulos_adicionados_por_AddJukerModules()
    {
        // Dado o host de teste com a composição real (sem substituir o registro de módulos compostos).
        // O tipo genérico só identifica o assembly do host (Juker.Host): "Program" é ambíguo com o do AppHost.
        await using var root = new WebApplicationFactory<ModuleDescriptor>();
        await using var factory = root.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(ConnectionStringKey, UnusedConnectionString);
        });

        // Quando o ComposedModules é resolvido do DI
        var composed = factory.Services.GetRequiredService<ComposedModules>();

        // Então contém exatamente os módulos adicionados por AddJukerModules, na ordem do ModuleCatalog
        composed.Names.Should().Equal(ModuleCatalog.All.Select(module => module.Name));

        // E há um único registro (AddJukerModules registra uma vez)
        factory.Services.GetServices<ComposedModules>().Should().ContainSingle();
    }
}
