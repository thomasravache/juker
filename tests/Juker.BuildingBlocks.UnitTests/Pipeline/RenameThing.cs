using Juker.Infrastructure;

namespace Juker.BuildingBlocks.UnitTests.Pipeline;

/// <summary>Comando de teste do UT-02: o validator exige <see cref="Name"/>.</summary>
public sealed record RenameThing(string Name) : ICommand<Guid>;
