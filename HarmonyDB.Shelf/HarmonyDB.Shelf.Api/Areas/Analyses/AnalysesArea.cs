using System.Security.Claims;
using System.Text.Json;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Areas.Analyses.Services;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Common.Items;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Analyses;

/// <summary>
/// The shelf's own analyses of recordings, for internal clients: served as a source of the source contract, version 2
/// (<c>/internal/source/</c> + the operation's name; scope <see cref="SourceScope"/>); put in — their content in the
/// source contract's kinds, then their files, streamed on into the music service's store —, removed, and gathered into a
/// public playlist (scope <see cref="WriteScope"/>). Everything here reads and writes the source contract's JSON options.
/// </summary>
public sealed class AnalysesArea : IShelfArea
{
    public const string SourceScope = "shelf.source";

    public const string WriteScope = "shelf.analyses.write";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AnalysesOptions>().Bind(configuration.GetSection(AnalysesOptions.SectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AnalysesOptions>, AnalysesOptionsValidator>());
        services.AddAudioBlobsClient(configuration);
        services.AddScoped<AnalysisStore>();
        services.AddScoped<AnalysesSource>();
        services.AddItemRows();
        services.AddScoped<AnalysesPlaylist>();
        services.AddHostedService<AnalysisBlobDeletionWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var source = endpoints.MapGroup("/internal/source").RequireInternalScope(SourceScope);

        source.MapPost($"/{SourceV2Urls.V2Search}", async (HttpRequest request, AnalysesSource service, CancellationToken cancellationToken)
            => V2(await service.SearchAsync(await ReadAsync<SearchRequest>(request, x => Valid(x.Caller) && x.Query != null, cancellationToken), cancellationToken))).WithName("AnalysesV2Search");

        source.MapPost($"/{SourceV2Urls.V2GetHeaders}", async (HttpRequest request, AnalysesSource service, CancellationToken cancellationToken)
            => V2(await service.GetHeadersAsync(await ReadAsync<GetHeadersRequest>(request, x => Valid(x.Caller) && x.Ids != null && x.Ids.All(id => id != null), cancellationToken), cancellationToken))).WithName("AnalysesV2GetHeaders");

        source.MapPost($"/{SourceV2Urls.V2GetContent}", async (HttpRequest request, AnalysesSource service, CancellationToken cancellationToken)
            => V2(await service.GetContentAsync(await ReadAsync<GetContentRequest>(request, x => Valid(x.Caller) && x.Id != null && x.Kinds != null, cancellationToken), cancellationToken))).WithName("AnalysesV2GetContent");

        source.MapPost($"/{SourceV2Urls.V2GetContents}", async (HttpRequest request, AnalysesSource service, CancellationToken cancellationToken)
            => V2(await service.GetContentsAsync(await ReadAsync<GetContentsRequest>(request, GetContentsRequest.IsValid, cancellationToken), cancellationToken))).WithName("AnalysesV2GetContents");

        source.MapPost($"/{SourceV2Urls.V2Resolve}", async (HttpRequest request, AnalysesSource service, CancellationToken cancellationToken)
            => V2(service.Resolve(await ReadAsync<ResolveRequest>(request, x => Valid(x.Caller) && x.Uris != null && x.Uris.All(uri => uri != null), cancellationToken)))).WithName("AnalysesV2Resolve");

        source.MapPost($"/{SourceV2Urls.V2GetIcon}", async (HttpRequest request, AnalysesSource service, CancellationToken cancellationToken) =>
            await service.GetIconAsync(await ReadAsync<GetIconRequest>(request, x => x.Name != null, cancellationToken), cancellationToken) is { } icon
                ? Results.File(icon.content, icon.contentType)
                : Results.NotFound()).WithName("AnalysesV2GetIcon");

        source.MapPost($"/{SourceV2Urls.V2Ping}", async (AnalysesSource service, CancellationToken cancellationToken) =>
        {
            await service.PingAsync(cancellationToken);
            return Results.Ok();
        }).WithName("AnalysesV2Ping");

        var analyses = endpoints.MapGroup("/internal/analyses").RequireInternalScope(WriteScope);

        analyses.MapPut("", async (HttpRequest request, ClaimsPrincipal user, AnalysisStore store, CancellationToken cancellationToken)
            => V2(await store.PutAsync(Client(user), await ReadAsync<PutAnalysisRequest>(request, x => x.Key != null && x.Title != null && x.Artists != null && x.Content != null, cancellationToken), cancellationToken))).WithName("PutAnalysis");

        analyses.MapPut("/{id}/files/{name}", async (string id, string name, HttpContext context, AnalysisStore store, IOptionsMonitor<AnalysesOptions> options, CancellationToken cancellationToken) =>
        {
            // a recording or a separate instrument is larger than the server takes by default
            context.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize = options.CurrentValue.MaxFileBytes;
            var request = context.Request;
            return V2(await store.PutFileAsync(Client(context.User), id, name, request.ContentType, request.Body, request.ContentLength, cancellationToken));
        }).WithName("PutAnalysisFile");

        analyses.MapGet("/{id}/files", async (string id, ClaimsPrincipal user, AnalysisStore store, CancellationToken cancellationToken)
            => V2(await store.GetFilesAsync(Client(user), id, cancellationToken))).WithName("GetAnalysisFiles");

        analyses.MapDelete("/{id}", async (string id, ClaimsPrincipal user, AnalysisStore store, CancellationToken cancellationToken) =>
        {
            await store.DeleteAsync(Client(user), id, cancellationToken);
            return Results.NoContent();
        }).WithName("DeleteAnalysis");

        analyses.MapPut("/playlist", async (HttpRequest request, AnalysesPlaylist playlist, CancellationToken cancellationToken)
            => V2(await playlist.PublishAsync(await ReadAsync<PublishAnalysesPlaylistRequest>(request, x => x.SystemAccount != null && x.Title != null && x.Ids != null && x.Ids.All(id => id != null), cancellationToken), cancellationToken))).WithName("PublishAnalysesPlaylist");
    }

    private static IResult V2<T>(T answer) => Results.Json(answer, SourceV2Json.Options);

    /// <summary>
    /// The request's body, which must be there and be whole: what the wire format leaves nullable but the request needs
    /// is checked by <paramref name="valid"/>. Anything else answers 400.
    /// </summary>
    private static async Task<T> ReadAsync<T>(HttpRequest request, Func<T, bool> valid, CancellationToken cancellationToken)
    {
        try
        {
            var body = await JsonSerializer.DeserializeAsync<T>(request.Body, SourceV2Json.Options, cancellationToken);
            return body != null && valid(body) ? body : throw ShelfException.BadRequest();
        }
        catch (JsonException)
        {
            throw ShelfException.BadRequest();
        }
    }

    private static bool Valid(Caller? caller) => caller is { Keys: not null } && caller.Keys.All(x => x != null);

    private static string Client(ClaimsPrincipal user) => user.FindFirst(InternalKeyDefaults.ClientClaimType)!.Value;
}
