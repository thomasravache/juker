namespace Juker.TestKit.Builders;

/// <summary>
/// Base dos builders de dados de teste (ADR-0012, regra 2): cada teste cria os próprios dados, com valores padrão
/// válidos e um bar (slug) único, e só sobrescreve o que importa para o cenário.
/// </summary>
/// <remarks>Interno ao harness (fora do contrato público do TestKit); visível para os projetos de IT.</remarks>
/// <typeparam name="T">O que o builder produz (entidade, comando ou requisição HTTP).</typeparam>
internal abstract class TestDataBuilder<T>
{
    /// <summary>Produz uma instância nova a cada chamada.</summary>
    public abstract T Build();

    /// <summary>Slug único por chamada, para isolar os dados de cada teste mesmo dentro do banco da classe.</summary>
    protected static string UniqueSlug(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 13)];
    }
}
