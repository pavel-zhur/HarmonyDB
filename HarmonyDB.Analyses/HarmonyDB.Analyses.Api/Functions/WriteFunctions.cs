using System.Security.Claims;
using System.Text.Json;
using HarmonyDB.Analyses.Api.Models;
using HarmonyDB.Analyses.Api.Services;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Source.Api.Model.V2;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OneShelf.Common.Api;

namespace HarmonyDB.Analyses.Api.Functions;

/// <summary>
/// Putting analyses in and taking them away, for internal clients with the scope <see cref="AnalysesOptions.WriteScope"/>:
/// the content first (<c>PUT analyses</c>), then each file it names (<c>PUT analyses/{id}/files/{name}</c>, the body
/// being the file). Everything is read and written with the source contract's JSON options; an error is answered as
/// <see cref="AnalysesError"/>. A client works with the analyses it put itself.
/// </summary>
public class WriteFunctions(AnalysisStore store, IOptions<AnalysesOptions> options)
{
    [Function("PutAnalysis", AuthorizationLevel.Internal, "put", Route = "analyses", InternalScope = AnalysesOptions.WriteScope)]
    public Task<IActionResult> Put(HttpRequest req, CancellationToken cancellationToken)
        => Handle(async () =>
        {
            PutAnalysisRequest? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync<PutAnalysisRequest>(req.Body, SourceV2Json.Options, cancellationToken);
            }
            catch (JsonException)
            {
                request = null;
            }

            if (request == null)
                throw new AnalysesException(StatusCodes.Status400BadRequest, AnalysesErrorCodes.BadRequest);

            return Json(await store.PutAsync(Client(req), request, cancellationToken));
        });

    [Function("PutAnalysisFile", AuthorizationLevel.Internal, "put", Route = "analyses/{id}/files/{name}", InternalScope = AnalysesOptions.WriteScope)]
    public Task<IActionResult> PutFile(HttpRequest req, string id, string name, CancellationToken cancellationToken)
        => Handle(async () =>
        {
            // a recording or a separate instrument may be larger than the application takes in by default: here the
            // module keeps to its own limit (by the length told, and by counting), and answers a file over it itself
            if (req.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
                feature.MaxRequestBodySize = null;

            return Json(await store.PutFileAsync(Client(req), id, name, req.ContentType, req.Body, req.ContentLength, options.Value.MaxFileBytes, cancellationToken));
        });

    [Function("GetAnalysisFiles", AuthorizationLevel.Internal, "get", Route = "analyses/{id}/files", InternalScope = AnalysesOptions.WriteScope)]
    public Task<IActionResult> GetFiles(HttpRequest req, string id, CancellationToken cancellationToken)
        => Handle(async () => Json(await store.GetFilesAsync(Client(req), id, cancellationToken)));

    [Function("DeleteAnalysis", AuthorizationLevel.Internal, "delete", Route = "analyses/{id}", InternalScope = AnalysesOptions.WriteScope)]
    public Task<IActionResult> Delete(HttpRequest req, string id, CancellationToken cancellationToken)
        => Handle(async () =>
        {
            await store.DeleteAsync(Client(req), id, cancellationToken);
            return new NoContentResult();
        });

    private static JsonResult Json<TResponse>(TResponse response) => new(response, SourceV2Json.Options);

    private static string Client(HttpRequest req) => req.HttpContext.User.FindFirstValue(InternalKeyDefaults.ClientClaimType)!;

    private static async Task<IActionResult> Handle(Func<Task<IActionResult>> execute)
    {
        try
        {
            return await execute();
        }
        catch (AnalysesException e)
        {
            return new JsonResult(e.Error, SourceV2Json.Options) { StatusCode = e.StatusCode };
        }
    }
}
