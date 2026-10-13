using NSubstitute;

namespace Juker.Modules.Catalog.UnitTests;

/// <summary>Controle negativo do ARQ-08: mockar outro tipo com a biblioteca de mock não é mock de HttpMessageHandler.</summary>
public static class SubstitutedDisposable
{
    public static IDisposable Create() => Substitute.For<IDisposable>();
}
