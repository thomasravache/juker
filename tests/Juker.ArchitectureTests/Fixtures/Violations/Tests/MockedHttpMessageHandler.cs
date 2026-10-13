using Moq;

namespace Juker.Modules.Catalog.UnitTests;

/// <summary>ARQ-08: mock de HttpMessageHandler com Moq (use o FakeHttpServer com WireMock.Net).</summary>
public static class MockedHttpMessageHandler
{
    public static HttpMessageHandler CreateHandler() => new Mock<HttpMessageHandler>().Object;
}
