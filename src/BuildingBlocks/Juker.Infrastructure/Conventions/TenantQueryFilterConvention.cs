using System.Linq.Expressions;
using System.Reflection;
using Juker.SharedKernel;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Juker.Infrastructure.Conventions;

internal sealed class TenantQueryFilterConvention(ModuleDbContext context) : IModelFinalizingConvention
{
    private static readonly MethodInfo ResolveMethod =
        typeof(ModuleDbContext).GetMethod(nameof(ModuleDbContext.ResolveTenantVenueId), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> conventionContext)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entity in modelBuilder.Metadata.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entity.ClrType))
            {
                var filter = BuildTenantFilter(entity.ClrType);
                entity.SetQueryFilter(filter);
            }
        }
    }

    private LambdaExpression BuildTenantFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var property = Expression.Property(parameter, nameof(ITenantScoped.VenueId));
        var contextConstant = Expression.Constant(context, typeof(ModuleDbContext));
        var resolveCall = Expression.Call(contextConstant, ResolveMethod);
        var body = Expression.Equal(property, resolveCall);
        return Expression.Lambda(body, parameter);
    }
}
