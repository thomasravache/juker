using System.Net;
using System.Net.Http.Json;

namespace Juker.TestKit.E2E;

/// <summary>
/// Cliente da API de controle de teste do host (<c>/_test/*</c>, só no ambiente E2E): monta pré-condições.
/// A jornada em teste é sempre feita pela interface (ADR-0013, regra 4).
/// </summary>
public sealed class TestControlClient
{
    private const string ClockAdvancePath = "/_test/clock/advance";
    private const string VenuesPath = "/_test/venues";

    private readonly HttpClient _http;

    internal TestControlClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Avança o relógio do servidor (<c>POST /_test/clock/advance</c>).</summary>
    public async Task AdvanceClockAsync(TimeSpan by)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);

        using var response = await _http.PostAsJsonAsync(new Uri(ClockAdvancePath, UriKind.Relative), new { seconds = by.TotalSeconds });
        await EnsureStatusAsync(response, HttpStatusCode.NoContent);
    }

    /// <summary>Cria um bar para o teste (<c>POST /_test/venues</c>; o endpoint do host é da SPEC-0007).</summary>
    public async Task<TestVenue> CreateVenueAsync(VenueSetup? setup = null)
    {
        var body = new { slug = setup?.Slug, name = setup?.Name };
        using var response = await _http.PostAsJsonAsync(new Uri(VenuesPath, UriKind.Relative), body);
        await EnsureStatusAsync(response, HttpStatusCode.Created);

        return await response.Content.ReadFromJsonAsync<TestVenue>()
            ?? throw new InvalidOperationException($"POST {VenuesPath} respondeu 201 sem corpo.");
    }

    private static async Task EnsureStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} respondeu {(int)response.StatusCode} " +
            $"(esperado {(int)expected}). O host está no ambiente E2E? Corpo: {content}");
    }
}
