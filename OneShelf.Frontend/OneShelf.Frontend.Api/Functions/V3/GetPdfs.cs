using System.Net;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneShelf.Authorization.Api.Client;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Compression;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Frontend.Api.AuthorizationQuickCheck;
using OneShelf.Frontend.Api.Model;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Services;
using OneShelf.Frontend.Database.Cosmos;
using OneShelf.Frontend.Database.Cosmos.Models;
using OneShelf.Pdfs.Api.Client;
using OneShelf.Pdfs.Generation.Inspiration.Models;
using OneShelf.Pdfs.Generation.Inspiration.Services;
using OneShelf.Pdfs.Generation.Volumes.Services;
using OneShelf.Pdfs.SpecificModel;
using GetPdfsRequest = OneShelf.Frontend.Api.Model.V3.Api.GetPdfsRequest;
using GetPdfsResponse = OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse;

namespace OneShelf.Frontend.Api.Functions.V3;

public class GetPdfs : AuthorizationFunctionBase<GetPdfsRequest, GetPdfsResponse>
{
    private readonly SongsDatabase _songsDatabase;
    private readonly CollectionReaderV3 _collectionReaderV3;
    private readonly VolumesGeneration _volumesGeneration;
    private readonly InspirationGeneration _inspirationGeneration;
    private readonly PdfsApiClient _pdfsApiClient;
    private readonly FrontendCosmosDatabase _frontendCosmosDatabase;
    private readonly AuthorizationQuickChecker _authorizationQuickChecker;
    private readonly SourceApiClient _sourceApiClient;

    public const string Version = "1";

    public GetPdfs(ILoggerFactory loggerFactory, SongsDatabase songsDatabase, CollectionReaderV3 collectionReaderV3, AuthorizationApiClient authorizationApiClient, VolumesGeneration volumesGeneration, InspirationGeneration inspirationGeneration, PdfsApiClient pdfsApiClient, FrontendCosmosDatabase frontendCosmosDatabase, AuthorizationQuickChecker authorizationQuickChecker, SourceApiClient sourceApiClient)
        : base(loggerFactory, authorizationApiClient)
    {
        _songsDatabase = songsDatabase;
        _collectionReaderV3 = collectionReaderV3;
        _volumesGeneration = volumesGeneration;
        _inspirationGeneration = inspirationGeneration;
        _pdfsApiClient = pdfsApiClient;
        _frontendCosmosDatabase = frontendCosmosDatabase;
        _authorizationQuickChecker = authorizationQuickChecker;
        _sourceApiClient = sourceApiClient;
    }

    [Function(ApiUrls.V3GetPdfs)]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req, [FromBody] GetPdfsRequest request) => await RunHandler(req, request);

    protected override async Task<GetPdfsResponse> Execute(HttpRequest httpRequest, GetPdfsRequest request)
    {
        if (!ArePdfsAllowed) throw new($"The pdfs generation is not allowed for this tenant, {TenantId}, userid = {request.Identity.Id}.");

        await _songsDatabase.Interactions.AddAsync(new()
        {
            UserId = request.Identity.Id,
            CreatedOn = DateTime.Now,
            InteractionType = InteractionType.PdfGeneration,
            Serialized = JsonConvert.SerializeObject(request),
            ShortInfoSerialized = (request.IncludeData, request.IncludeInspiration, request.Caption).ToString(),
        });
        await _songsDatabase.SaveChangesAsyncX();

        var collection = await _collectionReaderV3.Read(TenantId, request.Identity);
        var artists = collection.Artists.ToDictionary(x => x.Id);

        var versions = collection.Songs.SelectMany(s => s.Versions.Select(u => (s, u))).ToDictionary(x => x.u.Id);

        var requests = request.Versions.Select(x => (x, u: versions[x.VersionId]))
            .Where(x => x.u.u.ExternalId != null).Select(x => (x.u.u, x.u.s, x.x, r: new GetPdfsChunkRequestFile
            {
                Artists = string.Join(", ", x.u.s.Artists.Select(a => artists[a].Name)),
                Title = x.u.s.Title,
                ExternalId = x.u.u.ExternalId!,
                TwoColumns = x.x.TwoColumns,
                Source = x.u.u.Source ?? throw new("The source is expected. Could not have happened."),
                Transpose = x.x.Transpose,
                Alteration = x.x.Alteration,
            })).ToList();

        Dictionary<string, GetPdfsResponse.Pdf> allPdfs = new();
        foreach (var chunk in requests.Chunk(request.ChunkSize == 0 ? 20 : request.ChunkSize))
        {
            var pdfs = await ExecuteChunk(request.Identity, chunk.Select(x => x.r).ToList(), request.IncludeData, httpRequest);
            foreach (var pdf in pdfs)
            {
                allPdfs[pdf.Key] = new()
                {
                    PageCount = pdf.Value.PageCount,
                    PdfData = pdf.Value.Data,
                    PreviewLink = pdf.Value.PreviewLink,
                };
            }
        }

        var indexesMap = !request.Reindex ? null : requests.WithIndices().ToDictionary(x => x.x.s.Index, x => x.i + 1);

        byte[]? volume;
        if (request is { IncludeData: true, Caption: { } })
        {
            var allIndices = requests.Select(x => (x.r.ExternalId, x.s.Index)).GroupBy(x => x.ExternalId).ToDictionary(x => x.Key, x => x.Min(x => x.Index));

            var volumeDocuments = allPdfs
                .Select(x => (x.Value.PdfData!, (int?)allIndices[x.Key].SelectSingle(x => indexesMap?[x] ?? x)))
                .ToList();

            if (request.IncludeInspiration)
            {
                volumeDocuments.Insert(0, (await _inspirationGeneration.Inspiration(
                    TenantId,
                    InspirationDataOrdering.ByArtist,
                    false,
                    true,
                    false,
                    false,
                    allPdfs.Select(x => allIndices[x.Key]).ToList(), indexesMap), null));
            }

            volume = (await _volumesGeneration.CreateDocument(
                    volumeDocuments,
                    request.Caption,
                    DateTime.Now.Year.ToString().SelectSingle(y => request.Reindex ? $"{y}, reindexed" : y)))
                .bytes;
        }
        else
        {
            volume = null;
        }

        return new()
        {
            Pdfs = allPdfs,
            Volume = volume,
        };
    }

    private static string ToPdfConfiguration(GetPdfsChunkRequestFile x)
    {
        return $"{x.Artists} - {x.Title}, {x.TwoColumns}, {x.Source}, {x.Alteration}, {x.Transpose}";
    }

    private async Task<Dictionary<string, GetPdfsChunkResponseFile>> ExecuteChunk(Identity identity, List<GetPdfsChunkRequestFile> files, bool includeData, HttpRequest httpRequest)
    {
        var configurations = files.ToDictionary(x => x.ExternalId, ToPdfConfiguration);
        var requests = files.ToDictionary(x => x.ExternalId);

        var externalIdsAndConfigurations = files.Select(x => Pdf.GetId((x.ExternalId, configurations[x.ExternalId], Version))).ToList();
        var pdfs = !includeData ? null : await _frontendCosmosDatabase.GetPdfs(externalIdsAndConfigurations);
        var pageCounts = includeData ? null : await _frontendCosmosDatabase.GetPageCounts(externalIdsAndConfigurations);

        var result = new Dictionary<string, GetPdfsChunkResponseFile>();

        var toGenerate = new List<string>();
        var urlAbsolutePathBase = httpRequest.GetDisplayUrl().Replace(ApiUrls.V3GetPdfs, string.Empty);

        foreach (var pdfsRequestFile in files)
        {
            if (includeData)
            {
                var found = pdfs!.GetValueOrDefault(pdfsRequestFile.ExternalId);
                if (found == null)
                {
                    toGenerate.Add(pdfsRequestFile.ExternalId);
                    continue;
                }

                result[pdfsRequestFile.ExternalId] = new()
                {
                    Data = await CompressionTools.DecompressToBytes(found.Data),
                    PageCount = found.PageCount,
                    PreviewLink = await _authorizationQuickChecker.CreateV1PreviewPdfLink(identity, pdfsRequestFile, urlAbsolutePathBase),
                };
            }
            else
            {
                var found = pageCounts!.Safe(pdfsRequestFile.ExternalId);
                if (!found.HasValue)
                {
                    toGenerate.Add(pdfsRequestFile.ExternalId);
                    continue;
                }

                result[pdfsRequestFile.ExternalId] = new()
                {
                    PageCount = found.Value,
                    PreviewLink = await _authorizationQuickChecker.CreateV1PreviewPdfLink(identity, pdfsRequestFile, urlAbsolutePathBase),
                };
            }
        }

        var structures = (await _sourceApiClient.V1GetSongs(identity, files.Select(x => x.ExternalId).ToList())).Songs.Values;

        var tasks = new Dictionary<string, Task<(byte[] pdf, int pageCount)>>();
        foreach (Chords chords in structures)
        {
            if (!chords.IsStable) continue;

            var pdfsRequestFile = requests[chords.ExternalId];

            var html = chords.Output.AsHtml(new(transpose: pdfsRequestFile.Transpose, alteration: pdfsRequestFile.Alteration, isVariableWidth: chords.Output.IsVariableWidth));
            html = _pdfsApiClient.GenerateFinalHtml(html, pdfsRequestFile.Artists, pdfsRequestFile.Title, pdfsRequestFile.TwoColumns, pdfsRequestFile.Transpose, chords.SpecificAttributes.ToPdfsAttributes().ShortSourceName, pdfsRequestFile.Alteration);

            tasks[chords.ExternalId] = _pdfsApiClient.Convert(html);
        }

        await Task.WhenAll(tasks.Values);

        var compressed = new Dictionary<string, byte[]>();
        foreach (var (key, value) in tasks)
        {
            compressed[key] = await CompressionTools.Compress(value.Result.pdf);
            result[key] = new()
            {
                Data = includeData ? value.Result.pdf : null,
                PageCount = value.Result.pageCount,
                PreviewLink = await _authorizationQuickChecker.CreateV1PreviewPdfLink(identity, requests[key], urlAbsolutePathBase),
            };
        }

        await _frontendCosmosDatabase.AddOrUpdatePdfs(tasks.Select(x => new Pdf
        {
            Id = Pdf.GetId((x.Key, configurations[x.Key], Version)),
            ExternalId = x.Key,
            ExportConfiguration = configurations[x.Key],
            Version = Version,
            Data = compressed[x.Key],
            PageCount = x.Value.Result.pageCount,
            CreatedOn = DateTime.Now,
        }).ToList());

        return result;
    }

    private class GetPdfsChunkResponseFile
    {
        public required int PageCount { get; init; }

        public byte[]? Data { get; init; }

        public required string PreviewLink { get; init; }
    }
}