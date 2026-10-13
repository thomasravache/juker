using Juker.Infrastructure;
using Juker.SharedKernel;

namespace TestModules.Outbox;

/// <summary>Handler do comando de teste; resolvido do DI com os decorators (validação e logging); confirma a própria transação.</summary>
public sealed class CreateTestItemHandler(OutboxTestDbContext db) : ICommandHandler<CreateTestItem, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTestItem command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        db.Items.Add(TestItem.Create(command.ItemId, command.Venue, command.Name, command.EventId, command.OccurredAt));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result<Guid>.Success(command.ItemId);
    }
}
