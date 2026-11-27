using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.Api.Services.V2;
using HarmonyDB.Source.Api.Client.V2;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.V2;
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
        => SourceV2Requests.Handle<SearchRequest, SearchResponse>(req, SearchRequest.IsValid,
            r => relay.Search(r.Caller, r.Query, cancellationToken));

    [Function(SourceV2Urls.V2GetHeaders, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> GetHeaders(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetHeadersRequest, GetHeadersResponse>(req, GetHeadersRequest.IsValid,
            r => relay.GetHeaders(r.Caller, r.Ids, cancellationToken));

    [Function(SourceV2Urls.V2GetLabels, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> GetLabels(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetLabelsRequest, GetLabelsResponse>(req, GetLabelsRequest.IsValid,
            r => relay.GetLabels(r.Caller, r.Ids, cancellationToken));

    [Function(SourceV2Urls.V2GetContent, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> GetContent(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetContentRequest, GetContentResponse>(req, GetContentRequest.IsValid,
            r => relay.GetContent(r.Caller, r.Id, r.Kinds, r.Address, cancellationToken));

    [Function(SourceV2Urls.V2GetContents, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> GetContents(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetContentsRequest, GetContentsResponse>(req, GetContentsRequest.IsValid,
            r => relay.GetContents(r.Caller, r.Ids, r.Kinds, cancellationToken));

    [Function(SourceV2Urls.V2GetSources, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> GetSources(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetSourcesRequest, GetSourcesResponse>(req, GetSourcesRequest.IsValid,
            r => relay.GetSources(r.Caller, cancellationToken));

    [Function(SourceV2Urls.V2Resolve, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> Resolve(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<ResolveRequest, ResolveResponse>(req, ResolveRequest.IsValid,
            r => relay.Resolve(r.Caller, r.Uris, cancellationToken));

    /// <summary>
    /// A file from the source its id routes to, passed on as that source answers it: the status, the headers of
    /// <see cref="SourceFile.PassedHeaders"/>, and the body streamed for as long as it is read. 404 when the id is of no
    /// source or its source has no such file open to the caller, 503 when the source did not answer, 400 when the
    /// request is not one the contract takes.
    /// </summary>
    [Function(SourceV2Urls.V2GetFile, AuthorizationLevel.Internal, "get", "head", InternalScope = IndexInternalScopes.Index)]
    public async Task<IActionResult> GetFile(HttpRequest req, CancellationToken cancellationToken)
    {
        if (GetFileRequest.Decode(req.Headers[GetFileRequest.HeaderName].ToString()) is not { } request)
            return new BadRequestResult();

        SourceFile? file;
        try
        {
            file = await relay.GetFile(request, HttpMethods.IsHead(req.Method), req.Headers.Range.ToString(), req.Headers.IfRange.ToString(), cancellationToken);
        }
        catch (SourceUnavailableException)
        {
            return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
        }

        if (file == null) return new NotFoundResult();

        using (file)
        {
            var response = req.HttpContext.Response;
            response.StatusCode = file.StatusCode;
            foreach (var (name, values) in file.Headers)
                response.Headers[name] = values;

            if (!HttpMethods.IsHead(req.Method))
            {
                await using var body = await file.ReadBodyAsync(cancellationToken);
                await body.CopyToAsync(response.Body, cancellationToken);
            }
        }

        return new EmptyResult();
    }

    /// <summary>
    /// The picture from the source its name routes to; 404 when the name is of no source or the source has no such
    /// picture, 503 when the source did not answer.
    /// </summary>
    [Function(SourceV2Urls.V2GetIcon, AuthorizationLevel.Internal, "post", InternalScope = IndexInternalScopes.Index)]
    public async Task<IActionResult> GetIcon(HttpRequest req, CancellationToken cancellationToken)
    {
        try
        {
            return await SourceV2Requests.HandleResult<GetIconRequest>(req, GetIconRequest.IsValid,
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
