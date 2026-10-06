using Juker.Infrastructure.Conventions;
using Juker.Infrastructure.Outbox;
using Juker.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Juker.Infrastructure;

/// <summary>
/// Base do <c>DbContext</c> de cada módulo (ADR-0004, ADR-0008): schema do módulo, snake_case, filtro global de tenant
/// para <c>ITenantScoped</c> (falha fechado) e as entidades de outbox/inbox do módulo.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    protected ModuleDbContext(DbContextOptions options, ITenantContext tenant, string schema)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        _tenant = tenant;
        Schema = schema;
    }

    protected string Schema { get; }

    internal ITenantContext TenantContext => _tenant;

    internal VenueId ResolveTenantVenueId() =>
        _tenant.VenueId ?? throw new TenantNotResolvedException("Tenant não resolvido para entidade ITenantScoped.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages", Schema);
            b.HasKey(m => m.Id);
            b.Property(m => m.Id).ValueGeneratedNever();
            b.Property(m => m.Type).IsRequired();
            b.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
            b.Property(m => m.OccurredAt).IsRequired();
            b.Property(m => m.Status).IsRequired();
            b.Property(m => m.Attempts).IsRequired();
            b.Property(m => m.NextAttemptAt).IsRequired();
            b.Property(m => m.ProcessedAt);
            b.Property(m => m.LastError);
        });

        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("inbox_messages", Schema);
            b.HasKey(m => new { m.MessageId, m.Consumer });
            b.Property(m => m.MessageId);
            b.Property(m => m.Consumer).IsRequired();
            b.Property(m => m.ProcessedAt).IsRequired();
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
        configurationBuilder.Conventions.Add(_ => new TenantQueryFilterConvention(this));
    }
}
