namespace Juker.Infrastructure;

/// <summary>Gera identificadores Guid v7 a partir do <see cref="TimeProvider"/>.</summary>
public interface IIdGenerator
{
    Guid NewId();
}
