using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Juker.IntegrationTests.Testing;

/// <summary>
/// Host real do Juker num ambiente escolhido (<c>E2E</c>, <c>Staging</c>, <c>Production</c>) para o SPEC-0025:IT-02.
/// Diferente da <c>JukerApiFactory</c>, não troca o <see cref="TimeProvider"/>: o teste observa o que o host registra.
/// </summary>
/// <remarks>
/// O tipo de entrada é um tipo público do <c>Juker.Host</c> (a factory usa só o assembly dele): este projeto também
/// referencia o AppHost, que tem o próprio <c>Program</c>.
/// </remarks>
internal sealed class HostEnvironmentFactory(string connectionString, string environment) : WebApplicationFactory<Juker.Host.ModuleDescriptor>
{
    // Connection string do AppHost (SPEC-0002).
    private const string ConnectionStringKey = "ConnectionStrings:juker";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(environment);
        builder.UseSetting(ConnectionStringKey, connectionString);
    }
}
