using AwesomeAssertions;
using Juker.Infrastructure;
using Juker.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juker.BuildingBlocks.UnitTests.Pipeline;

[Trait("Category", "Unit")]
public sealed class CommandPipelineTests
{
    [Fact]
    [Trait("Spec", "SPEC-0004:UT-02")]
    public async Task Comando_invalido_nao_chama_o_handler_e_retorna_Validation_com_erro_por_campo()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dado o pipeline de decorators montado por AddJukerInfrastructure + AddModuleHandlers
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RenameThing, Guid>>();

        // Quando um comando com Name vazio é despachado
        var result = await handler.Handle(new RenameThing(string.Empty), ct);

        // Então o handler não é chamado e o resultado é Validation com Details["name"]
        provider.GetRequiredService<HandlerCalls>().Calls.Should().BeEmpty();
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Details.Should().NotBeNull();
        result.Error.Details!.Should().ContainKey("name");
        result.Error.Details["name"].Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0004:UT-02")]
    public async Task Comando_valido_chama_o_handler_uma_vez()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dado o pipeline de decorators montado por AddJukerInfrastructure + AddModuleHandlers
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RenameThing, Guid>>();
        var command = new RenameThing("Balcão");

        // Quando um comando válido é despachado
        var result = await handler.Handle(command, ct);

        // Então o handler é chamado exatamente uma vez e o resultado dele é devolvido
        provider.GetRequiredService<HandlerCalls>().Calls.Should().Equal(command);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(RenameThingHandler.RenamedId);
    }

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<HandlerCalls>();
        services.AddJukerInfrastructure(configuration);
        services.AddModuleHandlers(typeof(RenameThing).Assembly);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
