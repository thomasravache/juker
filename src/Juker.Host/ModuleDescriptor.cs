namespace Juker.Host;

public sealed record ModuleDescriptor(
    string Name,
    Func<IServiceCollection, IConfiguration, IServiceCollection> Add,
    Func<IEndpointRouteBuilder, IEndpointRouteBuilder> Map);
