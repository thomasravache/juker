using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juker.Infrastructure;

/// <summary>Montagem do kit no DI: infraestrutura comum, <c>DbContext</c> do módulo e handlers do módulo.</summary>
public static class InfrastructureRegistration
{
    /// <summary>
    /// TimeProvider.System (TryAdd), IIdGenerator (Guid v7), OutboxOptions (seção <c>Outbox</c>), IOutboxDrainer,
    /// OutboxDispatcher (hosted service só com <c>DispatcherEnabled</c>) e os decorators.
    /// </summary>
    public static IServiceCollection AddJukerInfrastructure(this IServiceCollection s, IConfiguration c) =>
        throw new NotImplementedException();

    /// <summary>Registra o contexto do módulo com Npgsql, o interceptor do outbox e o histórico de migrations no schema.</summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection s, string connectionName = "juker")
        where TContext : ModuleDbContext =>
        throw new NotImplementedException();

    /// <summary>Registra os handlers de comando e consulta (com decorators), os validators e os consumidores de eventos.</summary>
    public static IServiceCollection AddModuleHandlers(this IServiceCollection s, Assembly moduleAssembly) =>
        throw new NotImplementedException();
}
