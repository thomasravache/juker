using System.Reflection;
using AwesomeAssertions;
using Juker.E2ETests.Harness;
using Xunit;

namespace Juker.E2ETests.Conventions;

[Trait("Category", "Unit")]
public sealed class FixedWaitConventionTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly string Samples = typeof(FixedWaitSamples).FullName!;

    [Fact]
    [Trait("Spec", "SPEC-0025:UT-01")]
    public void Scanner_lista_os_metodos_que_chamam_Thread_Sleep_ou_Task_Delay()
    {
        // Dado métodos com Thread.Sleep (síncrono), Task.Delay no corpo de um async e numa lambda, e métodos sem espera fixa
        var methods = typeof(FixedWaitSamples).GetMethods(Declared);

        // Quando o IL das chamadas é analisado
        var callers = FixedWaitScanner.FindCallers(methods);

        // Então lista exatamente os três métodos com espera fixa
        callers.Should().BeEquivalentTo(
            $"{Samples}.{nameof(FixedWaitSamples.DormeComThreadSleep)}",
            $"{Samples}.{nameof(FixedWaitSamples.EsperaComTaskDelayNoCorpoAsync)}",
            $"{Samples}.{nameof(FixedWaitSamples.EsperaComTaskDelayNumaLambda)}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0025:UT-01")]
    public void Nenhum_metodo_de_teste_do_Juker_E2ETests_chama_Thread_Sleep_ou_Task_Delay()
    {
        // Dado os métodos de teste do assembly Juker.E2ETests (inclusive os E2E do harness)
        var tests = FixedWaitScanner.TestMethods(typeof(FixedWaitConventionTests).Assembly);
        tests.Select(method => method.Name).Should().Contain(
            [nameof(Nenhum_metodo_de_teste_do_Juker_E2ETests_chama_Thread_Sleep_ou_Task_Delay), nameof(HarnessTests.AppHost_e_navegadores_do_harness_ficam_prontos_no_ambiente_E2E)],
            "a convenção lê os testes reais do assembly");
        tests.Should().NotContain(method => method.DeclaringType == typeof(FixedWaitSamples), "as amostras não são testes");

        // Quando o IL das chamadas é analisado
        var callers = FixedWaitScanner.FindCallers(tests);

        // Então nenhum espera por tempo fixo; senão, a falha lista os métodos
        callers.Should().BeEmpty(
            "E2E esperam por condição (auto-wait/Expect), nunca por tempo fixo (ADR-0013, regra 9). Métodos: {0}",
            string.Join(", ", callers));
    }
}
