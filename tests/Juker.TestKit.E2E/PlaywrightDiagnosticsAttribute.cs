using System.Reflection;
using Xunit;
using Xunit.v3;

namespace Juker.TestKit.E2E;

/// <summary>
/// Gancho do diagnóstico de falha (SPEC-0025:IT-03, ADR-0013 regra 10): ao fim de cada teste, com o resultado já
/// conhecido, grava trace, vídeo e captura de tela das páginas do teste em <c>TestResults/playwright/&lt;teste&gt;/</c>
/// se ele falhou, ou descarta tudo se passou. Aplicado uma vez por assembly que usa a <see cref="PlaywrightFixture"/>:
/// <c>[assembly: PlaywrightDiagnostics]</c>. Interno (fora do contrato público).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class PlaywrightDiagnosticsAttribute : BeforeAfterTestAttribute
{
    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        ArgumentNullException.ThrowIfNull(test);

        // O xUnit roda este gancho depois do teste, com o resultado já no contexto do teste
        var failed = TestContext.Current.TestState?.Result == TestResult.Failed;
        PlaywrightDiagnostics.Complete(test.UniqueID, failed);
    }
}
