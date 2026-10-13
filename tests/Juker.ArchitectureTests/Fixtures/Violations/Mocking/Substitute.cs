namespace NSubstitute;

/// <summary>Simula o NSubstitute (ARQ-08, SPEC-0027:UT-01) sem referenciar o pacote: só a forma <c>Substitute.For&lt;T&gt;()</c>.</summary>
public static class Substitute
{
    public static T For<T>()
        where T : class =>
        throw new NotSupportedException("Fixture de arquitetura: nunca é executado.");
}
