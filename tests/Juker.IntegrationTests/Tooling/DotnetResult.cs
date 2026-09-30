namespace Juker.IntegrationTests.Tooling;

/// <summary>Código de saída e saída combinada (stdout + stderr) de um comando do CLI do .NET.</summary>
internal sealed record DotnetResult(int ExitCode, string Output);
