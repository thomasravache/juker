namespace Juker.ArchitectureTests.DesignTokens;

/// <summary>Cor literal ou <c>font-family</c> fora do arquivo de tokens.</summary>
/// <param name="File">Caminho completo do arquivo.</param>
/// <param name="Line">Linha (a partir de 1).</param>
/// <param name="Match">Trecho encontrado (ex.: <c>#ff007a</c>, <c>bg-[#fff]</c>, <c>font-family</c>).</param>
public sealed record DesignTokenViolation(string File, int Line, string Match);
