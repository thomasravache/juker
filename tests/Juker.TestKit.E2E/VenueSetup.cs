namespace Juker.TestKit.E2E;

/// <summary>Dados opcionais do bar criado por <see cref="TestControlClient.CreateVenueAsync"/>.</summary>
public sealed record VenueSetup(string? Slug = null, string? Name = null);
