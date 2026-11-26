using System.Text.Json;
using HarmonyDB.Analyses.Api.Models;
using HarmonyDB.Analyses.Api.Services;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using OneShelf.Common.Api;

namespace HarmonyDB.Analyses.Api.Functions;

/// <summary>
/// The analyses as a source of the contract, version 2 (see <see cref="AnalysesSource"/>), in the contract's one wire
/// format. Open only to internal clients with the scope <see cref="AnalysesOptions.SourceScope"/>. A request that
/// cannot be read, or lacks what the operation needs, is answered 400.
/// </summary>
public class SourceFunctions(AnalysesSource source)
{
    [Function(SourceV2Urls.V2Search, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> Search(HttpRequest req, CancellationToken cancellationToken)
        => Handle<SearchRequest>(req,
            r => IsComplete(r.Caller) && r.Query != null,
            async r => Json(await source.SearchAsync(r.Query, cancellationToken)));

    [Function(SourceV2Urls.V2GetHeaders, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> GetHeaders(HttpRequest req, CancellationToken cancellationToken)
        => Handle<GetHeadersRequest>(req,
            r => IsComplete(r.Caller) && r.Ids != null && r.Ids.All(x => !string.IsNullOrEmpty(x)),
            async r => Json(await source.GetHeadersAsync(r.Ids, cancellationToken)));

    [Function(SourceV2Urls.V2GetContent, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> GetContent(HttpRequest req, CancellationToken cancellationToken)
        => Handle<GetContentRequest>(req,
            r => IsComplete(r.Caller) && !string.IsNullOrEmpty(r.Id) && r.Kinds != null && r.Address is null or { IsAbsoluteUri: true },
            async r => Json((await source.GetContentsAsync([r.Id], r.Kinds, cancellationToken)).Single()));

    [Function(SourceV2Urls.V2GetContents, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> GetContents(HttpRequest req, CancellationToken cancellationToken)
        => Handle<GetContentsRequest>(req,
            GetContentsRequest.IsValid,
            async r => Json(new GetContentsResponse { Items = await source.GetContentsAsync(r.Ids, r.Kinds, cancellationToken) }));

    /// <summary>
    /// The one label of the source, the same for every caller.
    /// </summary>
    [Function(SourceV2Urls.V2GetSources, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> GetSources(HttpRequest req, CancellationToken cancellationToken)
        => Handle<GetSourcesRequest>(req,
            r => IsComplete(r.Caller),
            r => Task.FromResult<IActionResult>(Json(new GetSourcesResponse { Sources = [source.Label], Failures = [] })));

    [Function(SourceV2Urls.V2Resolve, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> Resolve(HttpRequest req, CancellationToken cancellationToken)
        => Handle<ResolveRequest>(req,
            r => IsComplete(r.Caller) && r.Uris != null && r.Uris.All(x => x is { IsAbsoluteUri: true }),
            async r => Json(await source.ResolveAsync(r.Uris, cancellationToken)));

    /// <summary>
    /// A file of an analysis, with ranges, from the analyses' folder; 404 when its content as given does not name it.
    /// </summary>
    [Function(SourceV2Urls.V2GetFile, AuthorizationLevel.Internal, "get", "head", InternalScope = AnalysesOptions.SourceScope)]
    public async Task<IActionResult> GetFile(HttpRequest req, CancellationToken cancellationToken)
    {
        if (GetFileRequest.Decode(req.Headers[GetFileRequest.HeaderName].ToString()) is not { } request)
            return new BadRequestResult();

        if (await source.GetFileAsync(request.Id, request.Name, cancellationToken) is not { } file)
            return new NotFoundResult();

        return new PhysicalFileResult(file.path, file.contentType)
        {
            EnableRangeProcessing = true,
            EntityTag = new EntityTagHeaderValue($"\"{file.sha256}\""),
            LastModified = file.storedAt,
        };
    }

    [Function(SourceV2Urls.V2GetIcon, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> GetIcon(HttpRequest req, CancellationToken cancellationToken)
        => Handle<GetIconRequest>(req,
            r => !string.IsNullOrEmpty(r.Name),
            async r => await source.GetIconAsync(r.Name, cancellationToken) is { } icon
                ? (IActionResult)new FileContentResult(icon.content, icon.contentType)
                : new NotFoundResult());

    /// <summary>
    /// Answers when the source works: its database answers.
    /// </summary>
    [Function(SourceV2Urls.V2Ping, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public async Task<IActionResult> Ping(CancellationToken cancellationToken)
        => await source.PingAsync(cancellationToken) ? new OkResult() : new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);

    private static JsonResult Json<TResponse>(TResponse response) => new(response, SourceV2Json.Options);

    private static bool IsComplete(Caller? caller) => Caller.IsValid(caller);

    private static async Task<IActionResult> Handle<TRequest>(HttpRequest httpRequest, Func<TRequest, bool> isComplete, Func<TRequest, Task<IActionResult>> execute)
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
}
