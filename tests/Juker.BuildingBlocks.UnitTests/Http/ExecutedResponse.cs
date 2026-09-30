using System.Text.Json;

namespace Juker.BuildingBlocks.UnitTests.Http;

/// <summary>Resposta HTTP produzida pela execução de um <c>IResult</c>.</summary>
internal sealed record ExecutedResponse(int StatusCode, string? ContentType, JsonElement? Body);
