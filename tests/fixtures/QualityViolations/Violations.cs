namespace QualityViolations;

public static class Violations
{
    public static DateTime Agora() => DateTime.Now;

    public static bool Iguais(string a, string b) => string.Equals(a, b);
}
