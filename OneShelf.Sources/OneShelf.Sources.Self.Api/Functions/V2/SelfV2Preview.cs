using System.Text.Json;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Contents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;
using OneShelf.Sources.Self.Api.Model;
using OneShelf.Sources.Self.Api.Model.V2;
using OneShelf.Sources.Self.Api.Models;
using OneShelf.Sources.Self.Api.Services;

namespace OneShelf.Sources.Self.Api.Functions.V2;

/// <summary>
/// The text of own chords shown as they will be once saved: the sheet of the source contract, version 2, in its wire
/// format, as the source gives it for saved own chords. Open only to internal clients with the scope
/// <see cref="SelfV2Options.PreviewScope"/>. A request that cannot be read is answered 400.
/// </summary>
public class SelfV2Preview(StructureParser structureParser)
{
    [Function(SelfApiUrls.V2Preview, AuthorizationLevel.Internal, "post", InternalScope = SelfV2Options.PreviewScope)]
    public async Task<IActionResult> Run(HttpRequest req, CancellationToken cancellationToken)
    {
        PreviewRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<PreviewRequest>(req.Body, SourceV2Json.Options, cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestResult();
        }

        if (request?.Contents == null) return new BadRequestResult();

        return new JsonResult(new SheetContent { Root = structureParser.ContentsToHtml(request.Contents) }, SourceV2Json.Options);
    }
}
