namespace Juker.Infrastructure;

/// <summary>Consulta a entidade <c>ITenantScoped</c> sem tenant resolvido (fail closed, ADR-0008).</summary>
public sealed class TenantNotResolvedException : InvalidOperationException
{
    public TenantNotResolvedException()
        : base("Tenant não resolvido.")
    {
    }

    public TenantNotResolvedException(string message)
        : base(message)
    {
    }

    public TenantNotResolvedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
