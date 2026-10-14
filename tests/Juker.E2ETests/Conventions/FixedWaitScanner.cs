using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Xunit;

namespace Juker.E2ETests.Conventions;

/// <summary>
/// Convenção "sem espera fixa" dos E2E (SPEC-0025:UT-01, ADR-0013 regra 9): lê o IL dos métodos e aponta os que
/// chamam <c>Thread.Sleep</c> ou <c>Task.Delay</c>, inclusive no corpo de métodos <c>async</c> (máquina de estados)
/// e em lambdas declaradas no método.
/// </summary>
/// <remarks>
/// A leitura segue as chamadas (<c>call</c>, <c>callvirt</c>, <c>newobj</c>, <c>ldftn</c>, <c>ldvirtftn</c>) para os
/// métodos do mesmo assembly: os gerados pelo compilador (máquinas de estados, lambdas, funções locais) e os auxiliares
/// que o teste usa (page objects, esperas por condição). Chamadas para fora do assembly não são seguidas.
/// </remarks>
internal static class FixedWaitScanner
{
    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opCode => opCode.Value);

    /// <summary>Os métodos de teste (<c>[Fact]</c>/<c>[Theory]</c>) declarados no assembly.</summary>
    public static IReadOnlyList<MethodInfo> TestMethods(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly.GetTypes()
            .SelectMany(type => type.GetMethods(AllDeclared))
            .Where(method => method.IsDefined(typeof(FactAttribute), inherit: true))
            .ToList();
    }

    /// <summary>
    /// Os métodos (entre <paramref name="methods"/>) que chamam <c>Thread.Sleep</c> ou <c>Task.Delay</c>, como
    /// <c>{Tipo.FullName}.{Método}</c>.
    /// </summary>
    public static IReadOnlyList<string> FindCallers(IEnumerable<MethodInfo> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);

        return methods
            .Where(method => CallsFixedWait(method, new HashSet<MethodBase>()))
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}")
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool CallsFixedWait(MethodBase method, HashSet<MethodBase> visited)
    {
        if (!visited.Add(method))
        {
            return false;
        }

        foreach (var callee in Callees(method))
        {
            if (IsFixedWait(callee))
            {
                return true;
            }

            if (callee.Module.Assembly == method.Module.Assembly && CallsFixedWait(callee, visited))
            {
                return true;
            }
        }

        // async/iteradores: o corpo fica no MoveNext da máquina de estados gerada
        return StateMachineMoveNext(method) is { } moveNext && CallsFixedWait(moveNext, visited);
    }

    private static bool IsFixedWait(MethodBase method) =>
        (method.DeclaringType == typeof(Thread) && string.Equals(method.Name, nameof(Thread.Sleep), StringComparison.Ordinal))
        || (method.DeclaringType == typeof(Task) && string.Equals(method.Name, nameof(Task.Delay), StringComparison.Ordinal));

    private static MethodInfo? StateMachineMoveNext(MethodBase method)
    {
        var stateMachine = method.GetCustomAttribute<StateMachineAttribute>()?.StateMachineType;
        return stateMachine?.GetMethod(nameof(IAsyncStateMachine.MoveNext), AllDeclared);
    }

    /// <summary>Os métodos referenciados pelas instruções de chamada do corpo de <paramref name="method"/>.</summary>
    private static List<MethodBase> Callees(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        var callees = new List<MethodBase>();
        if (il is null)
        {
            return callees;
        }

        var position = 0;
        while (position < il.Length)
        {
            short value = il[position++];
            if (value == 0xFE && position < il.Length)
            {
                value = (short)(0xFE00 | il[position++]);
            }

            if (!OpCodesByValue.TryGetValue(value, out var opCode))
            {
                // IL que não sabemos ler: não arrisca uma leitura desalinhada
                break;
            }

            if (opCode.OperandType == OperandType.InlineMethod && position + 4 <= il.Length)
            {
                var token = BitConverter.ToInt32(il, position);
                if (Resolve(method, token) is { } callee)
                {
                    callees.Add(callee);
                }
            }

            position += OperandSize(opCode, il, position);
        }

        return callees;
    }

    private static MethodBase? Resolve(MethodBase method, int token)
    {
        try
        {
            var typeArguments = method.DeclaringType is { IsGenericType: true } type ? type.GetGenericArguments() : null;
            var methodArguments = method is MethodInfo { IsGenericMethod: true } ? method.GetGenericArguments() : null;
            return method.Module.ResolveMethod(token, typeArguments, methodArguments);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static int OperandSize(OpCode opCode, byte[] il, int position) => opCode.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
        _ => 4,
    };
}
