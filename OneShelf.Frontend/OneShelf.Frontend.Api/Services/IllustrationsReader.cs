using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Songs.Hashing;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Model.V3.Illustrations;
using OneShelf.Illustrations.Api.Client;

namespace OneShelf.Frontend.Api.Services;

public class IllustrationsReader
{
    private readonly ILogger<IllustrationsReader> _logger;
    private readonly IllustrationsApiClient _illustrationsApiClient;
    private readonly SongsDatabase _songsDatabase;

    public IllustrationsReader(ILogger<IllustrationsReader> logger, IllustrationsApiClient illustrationsApiClient, SongsDatabase songsDatabase)
    {
        _logger = logger;
        _illustrationsApiClient = illustrationsApiClient;
        _songsDatabase = songsDatabase;
    }

    public enum Mode
    {
        AllVersionsOneTenantWithUnderGeneration,
        CollapseSongsIncludeTitles,
    }

    public async Task<IllustrationsResponse> Go(int? tenantId, Mode mode, int? eTag = null)
    {
        var all = await _illustrationsApiClient.All();
        var versionsByUri = (await _songsDatabase.Versions
                .SelectSingle(x => !tenantId.HasValue || mode == Mode.CollapseSongsIncludeTitles ? x : x.Where(x => x.Song.TenantId == tenantId))
                .Where(x => x.Song.Status == SongStatus.Live)
                .Include(x => x.Song)
                .ToListAsync())
            .ToLookup(x => x.Uri.ToString());

        var boundary = DateTime.Now.AddDays(-7);

        var underGeneration = mode == Mode.CollapseSongsIncludeTitles
            ? null
            : all
                .Responses
                .Where(x => x.Value.LatestCreatedOn >= boundary)
                .SelectMany(x => versionsByUri[x.Key].Select(y => (y.SongId, x.Value)))
                .GroupBy(x => x.SongId)
                .Select(x => x.Key)
                .ToList();

        var songIllustrationsMap = all
            .Responses
            .Where(x => x.Value.LatestCreatedOn < boundary)
            .SelectMany(x => versionsByUri[x.Key].Select(y => (version: y, response: x.Value)))
            .SelectSingle(songsAndResponses =>
            {
                if (mode == Mode.AllVersionsOneTenantWithUnderGeneration)
                {
                    return songsAndResponses
                        .GroupBy(x => x.version.SongId)
                        .Select(g => (songId: g.Key, illustrations: g.AsEnumerable()));
                }

                return songsAndResponses
                    .GroupBy(x => x.version.Uri)
                    .Select(g => g
                        .GroupBy(x => x.version.Song)
                        .OrderByDescending(v => v.Key.TenantId == tenantId)
                        .ThenBy(v => v.Key.Id)
                        .First())
                    .Select(x => (songId: x.Key.Id, illustrations: x.AsEnumerable()))
                    .GroupBy(x => x.songId)
                    .Select(g => g.MaxBy(g => g.illustrations.Count()));
            })
            .Select(
                x => (x.songId, value: new SongIllustrations
                {
                    EarliestCreatedOn = x.illustrations.Min(x => x.response.EarliestCreatedOn),
                    LatestCreatedOn = x.illustrations.Max(x => x.response.LatestCreatedOn),
                    Illustrations = x
                        .illustrations
                        .SelectMany(
                            r =>
                            {
                                return r.response.ImagePublicUrls
                                    .Where(x => x.Key > 0 && (mode == Mode.AllVersionsOneTenantWithUnderGeneration || !all.AlteredVersions.ContainsKey(x.Key) && x.Key == r.response.ImagePublicUrls.Keys.Where(k => !all.AlteredVersions.ContainsKey(k)).MaxBy(x1 => x1 switch
                                    {
                                        3 => -1,
                                        _ => x1
                                    })))
                                    .SelectMany(
                                        v => v.Value.SelectMany(
                                            (x, i) => x.Where(_ => mode == Mode.AllVersionsOneTenantWithUnderGeneration || i == 0).SelectMany(
                                                (y, j) => y
                                                    .Where((_, k) => mode == Mode.AllVersionsOneTenantWithUnderGeneration || k == 0)
                                                    .Select(
                                                        (z, k) => new SongIllustration
                                                        {
                                                            I = i,
                                                            J = j,
                                                            K = k,
                                                            Version = all.AlteredVersions.TryGetValue(v.Key, out var altered)
                                                                ? altered.BaseVersion
                                                                : v.Key,
                                                            AlterationTitle = all.AlteredVersions.TryGetValue(v.Key, out altered)
                                                                ? all.Alterations[altered.Key].Title
                                                                : null,
                                                            PublicUrls =
                                                                z.Url256 != null && z.Url1024 != null &&
                                                                z.Url128 != null && z.Url512 != null
                                                                    ? new()
                                                                    {
                                                                        Url256 = z.Url256,
                                                                        Url1024 = z.Url1024,
                                                                        Url128 = z.Url128,
                                                                        Url512 = z.Url512,
                                                                    }
                                                                    : null!
                                                        }))));
                            })
                        .Where(x => x.PublicUrls != null!)
                        .OrderBy(x => x.Version)
                        .ThenBy(x => x.I)
                        .ThenBy(x => x.J)
                        .ThenBy(x => x.K)
                        .ToList(),
                }))
            .Where(x => x.value.Illustrations.Any())
            .ToDictionary(x => x.songId, x => x.value);

        var hash = MyHashExtensions.CalculateHash(songIllustrationsMap.OrderBy(x => x.Key).SelectMany(x => new[]
        {
                x.Key,
                x.Value.EarliestCreatedOn.GetHashCode(),
                x.Value.LatestCreatedOn.GetHashCode(),
                x.Value.Illustrations.Count,
            }).Concat(underGeneration ?? Enumerable.Empty<int>()));

        if (hash == eTag)
        {
            return new()
            {
                Illustrations = null,
                Titles = null,
            };
        }

        Dictionary<int, string>? titles = null;
        if (mode == Mode.CollapseSongsIncludeTitles)
        {
            titles = (await _songsDatabase.Songs.Where(x => x.Status == SongStatus.Live).Include(x => x.Artists)
                    .ToListAsync())
                .Where(x => songIllustrationsMap.ContainsKey(x.Id))
                .ToDictionary(x => x.Id, x => $"{string.Join(", ", x.Artists.Select(x => x.Name))} — {x.Title}");
        }

        return new()
        {
            Illustrations = new()
            {
                Songs = songIllustrationsMap,
                UnderGeneration = underGeneration,
                Etag = hash,
            },
            Titles = titles,
        };
    }
}