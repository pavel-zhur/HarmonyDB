using System.Text.Json;
using HarmonyDB.Source.Api.Model.V2;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HarmonyDB.Index.Api.Functions.V2;

/// <summary>
/// Reads a request of the source contract, version 2, and writes its answer, in the contract's one wire format.
/// A request that cannot be read, or lacks what the operation needs, is answered 400.
/// </summary>
internal static class RelayV2Function
{
    public static Task<IActionResult> Handle<TRequest, TResponse>(HttpRequest httpRequest, Func<TRequest, bool> isComplete, Func<TRequest, Task<TResponse>> execute)
        where TRequest : class
        => HandleResult<TRequest>(httpRequest, isComplete, async request => new JsonResult(await execute(request), SourceV2Json.Options));

    public static async Task<IActionResult> HandleResult<TRequest>(HttpRequest httpRequest, Func<TRequest, bool> isComplete, Func<TRequest, Task<IActionResult>> execute)
        where TRequest : class
    {
        TRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<TRequest>(httpRequest.Body, SourceV2Json.Options, httpRequest.HttpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return new BadRequestResult();
        }

        if (request == null || !isComplete(request)) return new BadRequestResult();

        return await execute(request);
    }

    public static bool IsComplete(Caller? caller) => caller is { Keys: not null } && caller.Keys.All(x => x != null);
}
