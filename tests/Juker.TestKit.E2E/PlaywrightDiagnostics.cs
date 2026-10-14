using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Playwright;
using Xunit;
using Xunit.v3;

namespace Juker.TestKit.E2E;

/// <summary>Registro das gravações por teste e a decisão de guardar ou descartar o diagnóstico.</summary>
internal static class PlaywrightDiagnostics
{
    private const string TraceFile = "trace.zip";
    private const string VideoFile = "video.webm";
    private const string ScreenshotFile = "screenshot.png";
    private const int MaxDirectoryNameLength = 120;

    private static readonly ConcurrentDictionary<string, TestRecordings> Tests = new(StringComparer.Ordinal);

    /// <summary>Raiz do diagnóstico: <c>TestResults/playwright</c> na saída do projeto de teste.</summary>
    public static string ResultsRoot { get; } = Path.Combine(AppContext.BaseDirectory, "TestResults", "playwright");

    private static string StagingRoot { get; } = Path.Combine(Path.GetTempPath(), "juker-playwright");

    /// <summary>O teste em execução, conferindo que o assembly dele aplica o gancho do diagnóstico.</summary>
    public static RecordedTest? CurrentTest()
    {
        if (TestContext.Current.Test is not IXunitTest test)
        {
            return null;
        }

        var assembly = test.TestCase.TestClass.Class.Assembly;
        if (!assembly.IsDefined(typeof(PlaywrightDiagnosticsAttribute)))
        {
            throw new InvalidOperationException(
                $"O assembly {assembly.GetName().Name} usa a {nameof(PlaywrightFixture)} sem o gancho do diagnóstico de falha: " +
                "acrescente [assembly: Juker.TestKit.E2E.PlaywrightDiagnostics] (e o InternalsVisibleTo no Juker.TestKit.E2E.csproj).");
        }

        return new RecordedTest(test.UniqueID, test.TestDisplayName);
    }

    public static string NewStagingDirectory(string kind) =>
        Directory.CreateDirectory(Path.Combine(StagingRoot, $"{kind}-{Guid.NewGuid():N}")).FullName;

    public static void Register(PageRecording recording)
    {
        if (recording.Test is { } test)
        {
            Tests.GetOrAdd(test.UniqueId, _ => new TestRecordings(test.DisplayName)).Add(recording);
        }
    }

    /// <summary>Fim do teste: finaliza as páginas ainda abertas e resolve as já descartadas, conforme o resultado.</summary>
    public static void Complete(string testId, bool failed)
    {
        if (!Tests.TryRemove(testId, out var recordings))
        {
            return;
        }

        var (open, staged) = recordings.Complete(failed);
        foreach (var directory in staged)
        {
            Resolve(directory, recordings, failed);
        }

        foreach (var recording in open.Where(recording => recording.TryClaim()))
        {
            recording.Owner.Forget(recording);
            recording.Owner.Track(FinalizeAsync(recording, recordings, failed));
        }
    }

    /// <summary>
    /// Descarte da fixture com a página ainda aberta: grava o diagnóstico numa área temporária; o fim do teste decide
    /// (ou decide já, se o resultado chegou antes). Página sem teste dono é só fechada.
    /// </summary>
    public static async Task FinalizeOnDisposeAsync(PageRecording recording)
    {
        if (!recording.TryClaim())
        {
            return;
        }

        if (recording.Test is not { } test || !Tests.TryGetValue(test.UniqueId, out var recordings))
        {
            await DiscardAsync(recording);
            return;
        }

        var staged = await StageAsync(recording);
        if (recordings.Stage(staged) is { } failed)
        {
            Resolve(staged, recordings, failed);
        }
    }

    private static async Task FinalizeAsync(PageRecording recording, TestRecordings recordings, bool failed)
    {
        if (!failed)
        {
            await DiscardAsync(recording);
            return;
        }

        Resolve(await StageAsync(recording), recordings, failed);
    }

    /// <summary>Captura de tela, trace e vídeo da página numa pasta temporária; fecha o contexto.</summary>
    private static async Task<string> StageAsync(PageRecording recording)
    {
        var staging = NewStagingDirectory("diagnostics");
        try
        {
            if (!recording.Page.IsClosed)
            {
                await recording.Page.ScreenshotAsync(new() { Path = Path.Combine(staging, ScreenshotFile), FullPage = true });
            }

            await recording.Context.Tracing.StopAsync(new() { Path = Path.Combine(staging, TraceFile) });
        }
        finally
        {
            await recording.Context.CloseAsync();
        }

        if (recording.Page.Video is { } video)
        {
            await video.SaveAsAsync(Path.Combine(staging, VideoFile));
            await video.DeleteAsync();
        }

        DeleteDirectory(recording.VideoDirectory);
        return staging;
    }

    private static async Task DiscardAsync(PageRecording recording)
    {
        try
        {
            await recording.Context.Tracing.StopAsync();
        }
        catch (PlaywrightException)
        {
            // contexto já fechado pelo teste: não há trace para descartar
        }

        await recording.Context.CloseAsync();
        if (recording.Page.Video is { } video)
        {
            await video.DeleteAsync();
        }

        DeleteDirectory(recording.VideoDirectory);
    }

    /// <summary>Teste que falhou: move o diagnóstico para a pasta dele; teste que passou: apaga.</summary>
    private static void Resolve(string staged, TestRecordings recordings, bool failed)
    {
        if (failed)
        {
            var target = Directory.CreateDirectory(Path.Combine(ResultsRoot, DirectoryName(recordings.DisplayName))).FullName;
            var suffix = recordings.NextSuffix();
            foreach (var file in Directory.EnumerateFiles(staged))
            {
                var name = Path.GetFileNameWithoutExtension(file) + suffix + Path.GetExtension(file);
                File.Move(file, Path.Combine(target, name), overwrite: true);
            }
        }

        DeleteDirectory(staged);
    }

    /// <summary>O nome de exibição do teste como nome de pasta (sem caracteres inválidos e com tamanho limitado).</summary>
    private static string DirectoryName(string displayName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string([.. displayName.Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '_' : character)]);
        return name.Length <= MaxDirectoryNameLength ? name : name[..MaxDirectoryNameLength];
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>As gravações de um teste e, depois do fim dele, o resultado.</summary>
    private sealed class TestRecordings(string displayName)
    {
        private readonly Lock _lock = new();
        private readonly List<PageRecording> _open = [];
        private readonly List<string> _staged = [];
        private bool? _failed;
        private int _saved;

        public string DisplayName { get; } = displayName;

        public void Add(PageRecording recording)
        {
            lock (_lock)
            {
                _open.Add(recording);
            }
        }

        /// <summary>Guarda uma pasta temporária até o resultado; devolve o resultado se ele já chegou.</summary>
        public bool? Stage(string directory)
        {
            lock (_lock)
            {
                if (_failed is null)
                {
                    _staged.Add(directory);
                }

                return _failed;
            }
        }

        public (List<PageRecording> Open, List<string> Staged) Complete(bool failed)
        {
            lock (_lock)
            {
                _failed = failed;
                var result = (_open.ToList(), _staged.ToList());
                _open.Clear();
                _staged.Clear();
                return result;
            }
        }

        /// <summary>Sem sufixo para a primeira página do teste; <c>-2</c>, <c>-3</c>… para as seguintes.</summary>
        public string NextSuffix()
        {
            var index = Interlocked.Increment(ref _saved);
            return index == 1 ? string.Empty : $"-{index}";
        }
    }
}
