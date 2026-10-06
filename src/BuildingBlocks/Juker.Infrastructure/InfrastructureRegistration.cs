using System.Collections.Concurrent;
using System.Reflection;
using FluentValidation;
using Juker.Infrastructure.Common;
using Juker.Infrastructure.Outbox;
using Juker.Infrastructure.Pipeline;
using Juker.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juker.Infrastructure;

/// <summary>Montagem do kit no DI: infraestrutura comum, <c>DbContext</c> do módulo e handlers do módulo.</summary>
public static class InfrastructureRegistration
{
    private static readonly ConcurrentDictionary<Type, string> SchemaCache = new();

    /// <summary>
    /// TimeProvider.System (TryAdd), IIdGenerator (Guid v7), OutboxOptions (seção <c>Outbox</c>), IOutboxDrainer,
    /// OutboxDispatcher (hosted service só com <c>DispatcherEnabled</c>) e os decorators.
    /// </summary>
    public static IServiceCollection AddJukerInfrastructure(this IServiceCollection s, IConfiguration c)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(c);

        s.TryAddSingleton(TimeProvider.System);
        s.TryAddSingleton<IIdGenerator, IdGenerator>();

        s.Configure<OutboxOptions>(c.GetSection("Outbox"));
        var outboxOptions = c.GetSection("Outbox").Get<OutboxOptions>() ?? new OutboxOptions();

        s.TryAddSingleton<OutboxRegistry>();
        s.TryAddSingleton<OutboxProcessor>();
        s.TryAddSingleton<IOutboxDrainer, OutboxDrainer>();

        if (outboxOptions.DispatcherEnabled)
        {
            s.AddHostedService<OutboxDispatcher>();
        }

        return s;
    }

    /// <summary>Registra o contexto do módulo com Npgsql, o interceptor do outbox e o histórico de migrations no schema.</summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection s, string connectionName = "juker")
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var schema = GetSchemaForContext<TContext>();

        s.AddDbContext<TContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString(connectionName)
                ?? throw new InvalidOperationException($"Connection string '{connectionName}' não encontrada.");

            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            });
            options.AddInterceptors(new OutboxSaveChangesInterceptor());
            options.UseApplicationServiceProvider(null);
        });

        // Garante registro no OutboxRegistry
        s.AddSingleton(new ModuleDbContextDescriptor(typeof(TContext)));

        return s;
    }

    /// <summary>Registra os handlers de comando e consulta (com decorators), os validators e os consumidores de eventos.</summary>
    public static IServiceCollection AddModuleHandlers(this IServiceCollection s, Assembly moduleAssembly)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(moduleAssembly);

        foreach (var type in moduleAssembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            // Validators
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
            {
                s.AddTransient(iface, type);
            }

            // Command handlers
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)))
            {
                var args = iface.GetGenericArguments();
                RegisterCommandHandler(s, type, args[0], args[1]);
            }

            // Query handlers
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)))
            {
                var args = iface.GetGenericArguments();
                RegisterQueryHandler(s, type, args[0], args[1]);
            }

            // Event handlers
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
            {
                s.AddScoped(type);
                s.AddScoped(iface, type);
            }
        }

        return s;
    }

    private static string GetSchemaForContext<T>()
        where T : ModuleDbContext
    {
        return SchemaCache.GetOrAdd(typeof(T), type =>
        {
            var dummyOptions = new DbContextOptionsBuilder<T>().Options;
            try
            {
                var instance = (T)Activator.CreateInstance(type, dummyOptions, new FixedTenantContext(null))!;
                var prop = typeof(ModuleDbContext).GetProperty("Schema", BindingFlags.Instance | BindingFlags.NonPublic);
                return (string)prop!.GetValue(instance)!;
            }
            catch
            {
                var ctors = type.GetConstructors();
                foreach (var ctor in ctors)
                {
                    var parameters = ctor.GetParameters();
                    var args = new object?[parameters.Length];
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (typeof(DbContextOptions).IsAssignableFrom(parameters[i].ParameterType))
                        {
                            args[i] = dummyOptions;
                        }
                        else if (typeof(ITenantContext).IsAssignableFrom(parameters[i].ParameterType))
                        {
                            args[i] = new FixedTenantContext(null);
                        }
                        else if (parameters[i].ParameterType == typeof(string))
                        {
                            args[i] = "default";
                        }
                    }

                    try
                    {
                        var inst = (T)ctor.Invoke(args);
                        var prop = typeof(ModuleDbContext).GetProperty("Schema", BindingFlags.Instance | BindingFlags.NonPublic);
                        return (string)prop!.GetValue(inst)!;
                    }
                    catch
                    {
                        // Continua tentando outros construtores
                    }
                }

                return "public";
            }
        });
    }

    private static void RegisterCommandHandler(IServiceCollection services, Type handlerType, Type commandType, Type resultType)
    {
        services.AddScoped(handlerType);

        var serviceType = typeof(ICommandHandler<,>).MakeGenericType(commandType, resultType);
        services.AddScoped(serviceType, sp =>
        {
            var innerHandler = sp.GetRequiredService(handlerType);

            var loggingDecoratorType = typeof(LoggingCommandHandlerDecorator<,>).MakeGenericType(commandType, resultType);
            var loggerType = typeof(ILogger<>).MakeGenericType(loggingDecoratorType);
            var logger = sp.GetRequiredService(loggerType);
            var loggingHandler = Activator.CreateInstance(loggingDecoratorType, innerHandler, logger)!;

            var validatorInterfaceType = typeof(IValidator<>).MakeGenericType(commandType);
            var validators = sp.GetServices(validatorInterfaceType);

            var validationDecoratorType = typeof(ValidationCommandHandlerDecorator<,>).MakeGenericType(commandType, resultType);
            return Activator.CreateInstance(validationDecoratorType, loggingHandler, validators)!;
        });
    }

    private static void RegisterQueryHandler(IServiceCollection services, Type handlerType, Type queryType, Type resultType)
    {
        services.AddScoped(handlerType);

        var serviceType = typeof(IQueryHandler<,>).MakeGenericType(queryType, resultType);
        services.AddScoped(serviceType, sp =>
        {
            var innerHandler = sp.GetRequiredService(handlerType);

            var loggingDecoratorType = typeof(LoggingQueryHandlerDecorator<,>).MakeGenericType(queryType, resultType);
            var loggerType = typeof(ILogger<>).MakeGenericType(loggingDecoratorType);
            var logger = sp.GetRequiredService(loggerType);
            var loggingHandler = Activator.CreateInstance(loggingDecoratorType, innerHandler, logger)!;

            var validatorInterfaceType = typeof(IValidator<>).MakeGenericType(queryType);
            var validators = sp.GetServices(validatorInterfaceType);

            var validationDecoratorType = typeof(ValidationQueryHandlerDecorator<,>).MakeGenericType(queryType, resultType);
            return Activator.CreateInstance(validationDecoratorType, loggingHandler, validators)!;
        });
    }
}
