namespace Moq;

/// <summary>Simula o Moq (ARQ-08, SPEC-0027:UT-01) sem referenciar o pacote: só a forma <c>new Mock&lt;T&gt;()</c>.</summary>
/// <typeparam name="T">O tipo mockado.</typeparam>
public sealed class Mock<T>
    where T : class
{
    // Mesmo nome da API do Moq (Mock<T>.Object), para o fixture ter a forma real do uso
#pragma warning disable CA1720
    public T Object => throw new NotSupportedException("Fixture de arquitetura: nunca é executado.");
#pragma warning restore CA1720
}
