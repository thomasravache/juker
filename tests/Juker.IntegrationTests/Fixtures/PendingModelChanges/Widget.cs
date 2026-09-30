namespace Juker.IntegrationTests.PendingModelChanges;

public sealed class Widget
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Propriedade nova, sem migration: é a mudança pendente que o MigrationsGuard deve detectar.
    public string Color { get; set; } = string.Empty;
}
