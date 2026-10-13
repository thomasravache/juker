using NSubstitute;

namespace Juker.Modules.Catalog.UnitTests;

/// <summary>ARQ-08: mock de HttpMessageHandler com NSubstitute (use o FakeHttpServer com WireMock.Net).</summary>
public static class SubstitutedHttpMessageHandler
{
    public static HttpMessageHandler CreateHandler() => Substitute.For<HttpMessageHandler>();
}
