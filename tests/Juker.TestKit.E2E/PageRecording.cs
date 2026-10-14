using Microsoft.Playwright;

namespace Juker.TestKit.E2E;

/// <summary>Uma página criada pela <see cref="PlaywrightFixture"/>, com o contexto que grava trace e vídeo.</summary>
internal sealed class PageRecording(PlaywrightFixture owner, IBrowserContext context, IPage page, string videoDirectory, RecordedTest? test)
{
    private int _claimed;

    public PlaywrightFixture Owner { get; } = owner;

    public IBrowserContext Context { get; } = context;

    public IPage Page { get; } = page;

    public string VideoDirectory { get; } = videoDirectory;

    /// <summary>O teste que criou a página (nulo fora de um teste).</summary>
    public RecordedTest? Test { get; } = test;

    /// <summary>Quem finaliza a gravação (fim do teste ou descarte da fixture) a reclama uma única vez.</summary>
    public bool TryClaim() => Interlocked.Exchange(ref _claimed, 1) == 0;
}
