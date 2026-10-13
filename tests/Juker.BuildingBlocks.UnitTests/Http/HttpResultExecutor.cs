using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Juker.BuildingBlocks.UnitTests.Http;

/// <summary>Executa um <see cref="IResult"/> num <see cref="DefaultHttpContext"/> e lê a resposta (status, tipo e JSON).</summary>
internal static class HttpResultExecutor
{
    public static async Task<ExecutedResponse> ExecuteAsync(IResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;

        await result.ExecuteAsync(context);

        JsonElement? json = null;
        if (body.Length > 0)
        {
            body.Position = 0;
            using var document = await JsonDocument.ParseAsync(body);
            json = document.RootElement.Clone();
        }

        return new ExecutedResponse(context.Response.StatusCode, context.Response.ContentType, json);
    }
}
