using System.Net;

namespace Juker.Modules.Catalog.UnitTests;

/// <summary>ARQ-08: mock de HttpMessageHandler escrito à mão (use o FakeHttpServer com WireMock.Net).</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
}
