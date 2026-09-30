using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juker.Modules.Queue;

/// <summary>Ponto de registro do módulo Queue no host (chamado pelo ModuleCatalog do Juker.Host).</summary>
public static class QueueModule
{
    /// <summary>Registra os serviços do módulo. Ainda sem slices: nada a registrar.</summary>
    public static IServiceCollection AddQueueModule(this IServiceCollection s, IConfiguration c)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(c);
        return s;
    }

    /// <summary>Mapeia os endpoints do módulo. Ainda sem slices: nenhuma rota.</summary>
    public static IEndpointRouteBuilder MapQueueEndpoints(this IEndpointRouteBuilder e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return e;
    }
}
