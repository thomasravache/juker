namespace Juker.Infrastructure.Outbox;

internal sealed class OutboxRegistry(IEnumerable<ModuleDbContextDescriptor> descriptors)
{
    private readonly List<Type> _dbContextTypes = [.. descriptors.Select(d => d.DbContextType)];

    public IReadOnlyList<Type> DbContextTypes
    {
        get
        {
            lock (_dbContextTypes)
            {
                return [.. _dbContextTypes];
            }
        }
    }

    public void RegisterDbContextType(Type dbContextType)
    {
        ArgumentNullException.ThrowIfNull(dbContextType);
        lock (_dbContextTypes)
        {
            if (!_dbContextTypes.Contains(dbContextType))
            {
                _dbContextTypes.Add(dbContextType);
            }
        }
    }
}
