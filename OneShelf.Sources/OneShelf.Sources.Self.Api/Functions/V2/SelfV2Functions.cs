using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.V2;
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
        => SourceV2Requests.Handle<SearchRequest, SearchResponse>(req, SearchRequest.IsValid,
            r => source.Search(r.Caller, r.Query, cancellationToken));

    [Function(SourceV2Urls.V2GetHeaders, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> GetHeaders(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetHeadersRequest, GetHeadersResponse>(req, GetHeadersRequest.IsValid,
            r => source.GetHeaders(r.Caller, r.Ids, cancellationToken));

    /// <summary>
    /// The source's one label for every id of its form: nothing is read.
    /// </summary>
    [Function(SourceV2Urls.V2GetLabels, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> GetLabels(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetLabelsRequest, GetLabelsResponse>(req, GetLabelsRequest.IsValid,
            r => Task.FromResult(new GetLabelsResponse
            {
                Labels = r.Ids.Where(x => SelfV2Source.FromId(x) != null).Select(x => new ItemLabel { Id = x, Source = source.Label }).ToList(),
            }));

    [Function(SourceV2Urls.V2GetContent, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> GetContent(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetContentRequest, GetContentResponse>(req, GetContentRequest.IsValid,
            r => source.GetContent(r.Caller, r.Id, r.Kinds, r.Address, cancellationToken));

    [Function(SourceV2Urls.V2GetContents, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> GetContents(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetContentsRequest, GetContentsResponse>(req, GetContentsRequest.IsValid,
            r => source.GetContents(r.Caller, r.Ids, r.Kinds, cancellationToken));

    [Function(SourceV2Urls.V2GetSources, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> GetSources(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetSourcesRequest, GetSourcesResponse>(req, GetSourcesRequest.IsValid,
            _ => Task.FromResult(new GetSourcesResponse { Sources = [source.Label], Failures = [] }));

    [Function(SourceV2Urls.V2Resolve, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> Resolve(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<ResolveRequest, ResolveResponse>(req, ResolveRequest.IsValid,
            r => source.Resolve(r.Caller, r.Uris, cancellationToken));

    [Function(SourceV2Urls.V2GetIcon, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public Task<IActionResult> GetIcon(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.HandleResult<GetIconRequest>(req, GetIconRequest.IsValid,
            async r => await source.GetIcon(r.Name, cancellationToken) is { } icon
                ? (IActionResult)new FileContentResult(icon.Content, icon.ContentType)
                : new NotFoundResult());

    [Function(SourceV2Urls.V2Ping, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.InternalScope)]
    public IActionResult Ping() => new OkResult();
}
