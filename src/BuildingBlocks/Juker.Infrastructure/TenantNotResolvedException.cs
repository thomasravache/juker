namespace Juker.Infrastructure;

/// <summary>Consulta a entidade <c>ITenantScoped</c> sem tenant resolvido (fail closed, ADR-0008).</summary>
public sealed class TenantNotResolvedException : InvalidOperationException
{
    public TenantNotResolvedException() => throw new NotImplementedException();

    public TenantNotResolvedException(string message) => throw new NotImplementedException();

    public TenantNotResolvedException(string message, Exception innerException) => throw new NotImplementedException();
}
