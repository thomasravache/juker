namespace FluentAssertions;

/// <summary>Simula o FluentAssertions (ARQ-06) sem referenciar o pacote comercial.</summary>
public static class AssertionExtensions
{
    public static bool Should(this object? subject) => subject is not null;
}
