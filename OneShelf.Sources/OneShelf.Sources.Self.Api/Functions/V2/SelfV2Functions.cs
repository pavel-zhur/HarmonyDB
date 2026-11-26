using System.Text.Json;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;
using OneShelf.Sources.Self.Api.Models;
using OneShelf.Sources.Self.Api.Services.V2;

namespace OneShelf.Sources.Self.Api.Functions.V2;

/// <summary>
/// The own chords as a source of the contract, version 2 (see <see cref="SelfV2Source"/>), in the contract's one wire
/// format. Open only to internal clients with the scope <see cref="SelfV2Options.InternalScope"/>.
/// A request that cannot be read, or lacks what the operation needs, is answered 400.
/// </summary>
public class SelfV2Functions(SelfV2Source source)
{
    [Function(SourceV2Urls.V2Search, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> Search(HttpRequest req, CancellationToken cancellationToken)
        => Handle<SearchRequest>(req,
            r => IsComplete(r.Caller) && !string.IsNullOrWhiteSpace(r.Query),
            async r => Json(await source.Search(r.Caller, r.Query, cancellationToken)));

    [Function(SourceV2Urls.V2GetHeaders, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> GetHeaders(HttpRequest req, CancellationToken cancellationToken)
        => Handle<GetHeadersRequest>(req,
            r => IsComplete(r.Caller) && r.Ids != null && r.Ids.All(x => !string.IsNullOrEmpty(x)),
            async r => Json(await source.GetHeaders(r.Caller, r.Ids, cancellationToken)));

    [Function(SourceV2Urls.V2GetContent, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> GetContent(HttpRequest req, CancellationToken cancellationToken)
        => Handle<GetContentRequest>(req,
            r => IsComplete(r.Caller) && !string.IsNullOrEmpty(r.Id) && r.Kinds != null && r.Address is null or { IsAbsoluteUri: true },
            async r => Json(await source.GetContent(r.Caller, r.Id, r.Kinds, r.Address, cancellationToken)));

    [Function(SourceV2Urls.V2GetContents, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> GetContents(HttpRequest req, CancellationToken cancellationToken)
        => Handle<GetContentsRequest>(req,
            GetContentsRequest.IsValid,
            async r => Json(await source.GetContents(r.Caller, r.Ids, r.Kinds, cancellationToken)));

    [Function(SourceV2Urls.V2GetSources, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> GetSources(HttpRequest req, CancellationToken cancellationToken)
        => Handle<GetSourcesRequest>(req,
            r => IsComplete(r.Caller),
            r => Task.FromResult<IActionResult>(Json(new GetSourcesResponse { Sources = [source.Label], Failures = [] })));

    [Function(SourceV2Urls.V2Resolve, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> Resolve(HttpRequest req, CancellationToken cancellationToken)
        => Handle<ResolveRequest>(req,
            r => IsComplete(r.Caller) && r.Uris != null && r.Uris.All(x => x is { IsAbsoluteUri: true }),
            async r => Json(await source.Resolve(r.Caller, r.Uris, cancellationToken)));

    [Function(SourceV2Urls.V2GetIcon, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> GetIcon(HttpRequest req, CancellationToken cancellationToken)
        => Handle<GetIconRequest>(req,
            r => !string.IsNullOrEmpty(r.Name),
            async r => await source.GetIcon(r.Name, cancellationToken) is { } icon
                ? (IActionResult)new FileContentResult(icon.Content, icon.ContentType)
                : new NotFoundResult());

    [Function(SourceV2Urls.V2Ping, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public IActionResult Ping() => new OkResult();

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
