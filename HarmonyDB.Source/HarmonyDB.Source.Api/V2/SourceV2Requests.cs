using System.Text.Json;
using HarmonyDB.Source.Api.Model.V2;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HarmonyDB.Source.Api.V2;

/// <summary>
/// The serving side of the source contract, version 2: a request read and its answer written in the contract's one wire
/// format, the same for every source and for the index's relay. A request that cannot be read, or is not one the
/// contract takes (by its own rule, e.g. <c>GetContentRequest.IsValid</c>), is answered 400.
/// </summary>
public static class SourceV2Requests
{
    /// <summary>
    /// Reads the request and, when it is one the contract takes, answers with what <paramref name="execute"/> gives,
    /// as JSON.
    /// </summary>
    public static Task<IActionResult> Handle<TRequest, TResponse>(HttpRequest httpRequest, Func<TRequest, bool> isValid, Func<TRequest, Task<TResponse>> execute)
        where TRequest : class
        => HandleResult<TRequest>(httpRequest, isValid, async request => Answer(await execute(request)));

    /// <summary>
    /// Reads the request and, when it is one the contract takes, answers with what <paramref name="execute"/> gives.
    /// </summary>
    public static async Task<IActionResult> HandleResult<TRequest>(HttpRequest httpRequest, Func<TRequest, bool> isValid, Func<TRequest, Task<IActionResult>> execute)
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

        if (request == null || !isValid(request)) return new BadRequestResult();

        return await execute(request);
    }

    /// <summary>
    /// An answer in the contract's wire format.
    /// </summary>
    public static JsonResult Answer<TResponse>(TResponse response) => new(response, SourceV2Json.Options);
}
