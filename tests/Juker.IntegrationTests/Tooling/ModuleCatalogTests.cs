using AwesomeAssertions;
using Juker.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Juker.IntegrationTests.Tooling;

[Trait("Category", "Unit")]
public sealed class ModuleCatalogTests
{
    private static readonly string[] ExpectedModules = ["Venues", "Sessions", "Catalog", "Queue", "Playback"];

    [Fact]
    [Trait("Spec", "SPEC-0002:UT-01")]
    public async Task Catalogo_tem_os_cinco_modulos_em_ordem_e_cada_registro_executa_sem_excecao()
    {
        // Dado o catálogo de módulos do host
        var modules = ModuleCatalog.All;

        // Então contém exatamente os cinco módulos, nesta ordem
        modules.Select(module => module.Name).Should().Equal(ExpectedModules);
        modules.Should().AllSatisfy(module =>
        {
            module.Add.Should().NotBeNull();
            module.Map.Should().NotBeNull();
        });

        // E cada Add/Map executa sem exceção num host de teste
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:juker"] = "Host=localhost;Database=juker;Username=juker;Password=juker",
        });

        foreach (var module in modules)
        {
            var add = () => module.Add(builder.Services, builder.Configuration);
            add.Should().NotThrow($"Add{module.Name}Module deve registrar o módulo sem exceção");
        }

        await using var app = builder.Build();

        foreach (var module in modules)
        {
            var map = () => module.Map(app);
            map.Should().NotThrow($"Map{module.Name}Endpoints deve mapear o módulo sem exceção");
        }
    }
}
