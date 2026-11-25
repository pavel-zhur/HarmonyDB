using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.Api.Services.V2;
using HarmonyDB.Source.Api.Client.V2;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;

namespace HarmonyDB.Index.Api.Functions.V2;

/// <summary>
/// The source contract, version 2, served by relaying it to the configured sources (see <see cref="SourceV2Relay"/>).
/// Open only to internal clients with the scope <see cref="IndexInternalScopes.Index"/>.
/// </summary>
public class RelayV2Functions(SourceV2Relay relay)
{
    [Function(SourceV2Urls.V2Search, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> Search(HttpRequest req, CancellationToken cancellationToken)
        => RelayV2Function.Handle<SearchRequest, SearchResponse>(req,
            r => RelayV2Function.IsComplete(r.Caller) && !string.IsNullOrWhiteSpace(r.Query),
            r => relay.Search(r.Caller, r.Query, cancellationToken));

    [Function(SourceV2Urls.V2GetHeaders, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> GetHeaders(HttpRequest req, CancellationToken cancellationToken)
        => RelayV2Function.Handle<GetHeadersRequest, GetHeadersResponse>(req,
            r => RelayV2Function.IsComplete(r.Caller) && r.Ids != null && r.Ids.All(x => !string.IsNullOrEmpty(x)),
            r => relay.GetHeaders(r.Caller, r.Ids, cancellationToken));

    [Function(SourceV2Urls.V2GetContent, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> GetContent(HttpRequest req, CancellationToken cancellationToken)
        => RelayV2Function.Handle<GetContentRequest, GetContentResponse>(req,
            r => RelayV2Function.IsComplete(r.Caller) && !string.IsNullOrEmpty(r.Id) && r.Kinds != null,
            r => relay.GetContent(r.Caller, r.Id, r.Kinds, cancellationToken));

    [Function(SourceV2Urls.V2Resolve, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> Resolve(HttpRequest req, CancellationToken cancellationToken)
        => RelayV2Function.Handle<ResolveRequest, ResolveResponse>(req,
            r => RelayV2Function.IsComplete(r.Caller) && r.Uris != null && r.Uris.All(x => x is { IsAbsoluteUri: true }),
            r => relay.Resolve(r.Caller, r.Uris, cancellationToken));

    /// <summary>
    /// The picture from the source its name routes to; 404 when the name is of no source or the source has no such
    /// picture, 503 when the source did not answer.
    /// </summary>
    [Function(SourceV2Urls.V2GetIcon, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public async Task<IActionResult> GetIcon(HttpRequest req, CancellationToken cancellationToken)
    {
        try
        {
            return await RelayV2Function.HandleResult<GetIconRequest>(req,
                r => !string.IsNullOrEmpty(r.Name),
                async r => await relay.GetIcon(r.Name, cancellationToken) is { } icon
                    ? (IActionResult)new FileContentResult(icon.Content, icon.ContentType)
                    : new NotFoundResult());
        }
        catch (SourceUnavailableException)
        {
            return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// Answers when the relay works. The sources are not asked: each operation reports the sources that did not answer.
    /// </summary>
    [Function(SourceV2Urls.V2Ping, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public IActionResult Ping() => new OkResult();
}
