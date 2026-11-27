using HarmonyDB.Analyses.Api.Models;
using HarmonyDB.Analyses.Api.Services;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.V2;
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
        => SourceV2Requests.Handle<SearchRequest, SearchResponse>(req, SearchRequest.IsValid,
            r => source.SearchAsync(r.Query, cancellationToken));

    [Function(SourceV2Urls.V2GetHeaders, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> GetHeaders(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetHeadersRequest, GetHeadersResponse>(req, GetHeadersRequest.IsValid,
            r => source.GetHeadersAsync(r.Ids, cancellationToken));

    /// <summary>
    /// The one label of the source for every id of its form: nothing is read.
    /// </summary>
    [Function(SourceV2Urls.V2GetLabels, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> GetLabels(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetLabelsRequest, GetLabelsResponse>(req, GetLabelsRequest.IsValid,
            r => Task.FromResult(new GetLabelsResponse
            {
                Labels = r.Ids.Where(x => x.StartsWith(AnalysisStore.IdPrefix, StringComparison.Ordinal)).Select(x => new ItemLabel { Id = x, Source = source.Label }).ToList(),
            }));

    [Function(SourceV2Urls.V2GetContent, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> GetContent(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetContentRequest, GetContentResponse>(req, GetContentRequest.IsValid,
            async r => (await source.GetContentsAsync([r.Id], r.Kinds, cancellationToken)).Single());

    [Function(SourceV2Urls.V2GetContents, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> GetContents(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetContentsRequest, GetContentsResponse>(req, GetContentsRequest.IsValid,
            async r => new GetContentsResponse { Items = await source.GetContentsAsync(r.Ids, r.Kinds, cancellationToken) });

    /// <summary>
    /// The one label of the source, the same for every caller.
    /// </summary>
    [Function(SourceV2Urls.V2GetSources, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> GetSources(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<GetSourcesRequest, GetSourcesResponse>(req, GetSourcesRequest.IsValid,
            _ => Task.FromResult(new GetSourcesResponse { Sources = [source.Label], Failures = [] }));

    [Function(SourceV2Urls.V2Resolve, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public Task<IActionResult> Resolve(HttpRequest req, CancellationToken cancellationToken)
        => SourceV2Requests.Handle<ResolveRequest, ResolveResponse>(req, ResolveRequest.IsValid,
            r => source.ResolveAsync(r.Uris, cancellationToken));

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
        => SourceV2Requests.HandleResult<GetIconRequest>(req, GetIconRequest.IsValid,
            async r => await source.GetIconAsync(r.Name, cancellationToken) is { } icon
                ? (IActionResult)new FileContentResult(icon.content, icon.contentType)
                : new NotFoundResult());

    /// <summary>
    /// Answers when the source works: its database answers.
    /// </summary>
    [Function(SourceV2Urls.V2Ping, AuthorizationLevel.Internal, "post", InternalScope = AnalysesOptions.SourceScope)]
    public async Task<IActionResult> Ping(CancellationToken cancellationToken)
        => await source.PingAsync(cancellationToken) ? new OkResult() : new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
}
