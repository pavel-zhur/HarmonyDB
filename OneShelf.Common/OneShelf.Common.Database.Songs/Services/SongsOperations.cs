using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Songs;
using Version = OneShelf.Common.Database.Songs.Model.Version;

namespace OneShelf.Common.Database.Songs.Services;

public class SongsOperations
{
    private readonly ILogger<SongsOperations> _logger;

    public SongsOperations(ILogger<SongsOperations> logger, SongsDatabase songsDatabase)
    {
        _logger = logger;
        SongsDatabase = songsDatabase;
    }

    public SongsDatabase SongsDatabase { get; }

    public async Task<(bool songExisted, bool versionExisted, Song song)> VersionImport(int tenantId, Uri uri, IReadOnlyList<string> artists, string? title, long userId, byte likeLevel, DateTime? happenedOn = null, int? likeCategoryId = null, int transpose = 0)
    {
        await using var transaction = await SongsDatabase.Database.BeginTransactionAsync();

        if (!artists.Any()) throw new("No artists.");
        artists = artists.Select(x => x.ToLowerInvariant().Trim()).ToList();
        if (artists.Any(string.IsNullOrWhiteSpace)) throw new("Empty artists.");

        title = title?.ToLowerInvariant().Trim();
        if (string.IsNullOrWhiteSpace(title)) throw new("No title.");

        var existingVersions = await SongsDatabase.Versions.Include(x => x.Song).Where(x => x.Uri == uri && x.Song.Status == SongStatus.Live && x.Song.TenantId == tenantId).ToListAsync();
        if (existingVersions.Any())
            return (true, true, existingVersions.First().Song);

        var foundArtists = await FindOrCreateArtists(tenantId, artists);

        var matchingSongs = await SongsDatabase.Songs.Where(x => x.Title == title && x.TenantId == tenantId).Include(x => x.Artists)
            .ToListAsync();
        matchingSongs = matchingSongs
            .Where(x => x.Artists.Select(x => x.Id).OrderBy(x => x).SequenceEqual(foundArtists.Select(x => x.Id).OrderBy(x => x)))
            .ToList();

        if (matchingSongs.Count > 1)
        {
            throw new("Multiple songs found.");
        }

        happenedOn ??= DateTime.Now;

        Song foundSong;
        bool songExisted;
        if (matchingSongs.Count == 1)
        {
            songExisted = true;
            foundSong = matchingSongs[0];
        }
        else
        {
            songExisted = false;
            var newIndex = await SongsDatabase.GetNextSongIndex(tenantId);

            foundSong = new()
            {
                TenantId = tenantId,
                Title = title,
                Artists = foundArtists,
                Status = SongStatus.Live,
                Index = newIndex,
                CreatedByUserId = userId,
                CreatedOn = happenedOn.Value,
                SourceUniqueIdentifier = $"web-{Guid.NewGuid()}",
            };
            SongsDatabase.Songs.Add(foundSong);
            await SongsDatabase.SaveChangesAsyncX(true);
        }

        var version = new Version
        {
            SongId = foundSong.Id,
            Uri = uri,
            CreatedOn = happenedOn.Value,
            UserId = userId,
        };
        SongsDatabase.Versions.Add(version);
        await SongsDatabase.SaveChangesAsyncX();

        if (likeCategoryId.HasValue)
        {
            var likeCategory = await SongsDatabase.LikeCategories.SingleOrDefaultAsync(x => x.Id == likeCategoryId);
            if (likeCategory == null ||
                likeCategory.Access is LikeCategoryAccess.Private or LikeCategoryAccess.SharedView &&
                likeCategory.UserId != userId)
            {
                _logger.LogWarning(
                    "The category is inaccessible or does not exist, category id = {categoryId}, user id = {userId}.",
                    likeCategoryId,
                    userId);
                likeCategoryId = null;
                likeLevel = 0;
            }
        }

        var like = new Like
        {
            CreatedOn = happenedOn.Value,
            UserId = userId,
            SongId = foundSong.Id,
            VersionId = version.Id,
            Transpose = Math.Max(-7, Math.Min(7, transpose)),
            Level = Math.Max((byte)0, Math.Min(SongsConstants.MaxLevel, likeLevel)),
            LikeCategoryId = likeCategoryId,
        };

        SongsDatabase.Likes.Add(like);

        await SongsDatabase.SaveChangesAsyncX();

        await transaction.CommitAsync();

        return (songExisted, false, foundSong);
    }

    public async Task<List<Artist>> FindOrCreateArtists(int tenantId, IReadOnlyList<string> artists)
    {
        artists = artists.Select(x => x.ToLowerInvariant().Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        var foundArtists = new List<Artist>();
        foreach (var artistTitle in artists)
        {
            var foundArtist = await SongsDatabase.Artists
                .Where(x => x.Name == artistTitle || x.Synonyms.Any(s => s.Title == artistTitle)).Where(x => x.TenantId == tenantId).FirstOrDefaultAsync();

            if (foundArtist == null)
            {
                foundArtist = new()
                {
                    Name = artistTitle,
                    TenantId = tenantId,
                };
                SongsDatabase.Artists.Add(foundArtist);
                await SongsDatabase.SaveChangesAsyncX();
            }

            foundArtists.Add(foundArtist);
        }

        return foundArtists;
    }

    public async Task InitializeTenant(int tenantId)
    {
        try
        {
            await using var transaction = await SongsDatabase.Database.BeginTransactionAsync();

            var user = await SongsDatabase.Users.Include(x => x.Tenant).SingleAsync(x => x.TenantId == tenantId);

            var versions = await SongsDatabase.Versions.Where(x => x.IsDefaultTemplate)
                .Include(x => x.Song)
                .ThenInclude(x => x.Artists)
                .ThenInclude(x => x.Synonyms)
                .Include(x => x.Song)
                .ThenInclude(x => x.Likes)
                .Where(v => v.Song.Index < 1000 && v.Song.Status == SongStatus.Live)
                .ToListAsync();

            var artists = versions.SelectMany(v => v.Song.Artists).Distinct().ToDictionary(a => a, a => new Artist
            {
                TenantId = tenantId,
                CategoryOverride = a.CategoryOverride,
                Name = a.Name,
                Synonyms = a.Synonyms.Select(s => new ArtistSynonym
                {
                    Title = s.Title,
                }).ToList(),
            });
            SongsDatabase.Artists.AddRange(artists.Values);
            await SongsDatabase.SaveChangesAsyncX();

            var songs = versions.Select(v => v.Song).Distinct().ToDictionary(s => s, s => new Song
            {
                TenantId = tenantId,
                Index = s.Index,
                CreatedByUser = user,
                CreatedOn = DateTime.Now,
                SourceUniqueIdentifier = Guid.NewGuid().ToString(),
                Status = SongStatus.Live,
                CategoryOverride = s.CategoryOverride,
                Title = s.Title,
                Artists = s.Artists.Select(a => artists[a]).ToList(),
                TemplateRating = (float)s.Likes.GetRating(),
            });
            SongsDatabase.Songs.AddRange(songs.Values);
            await SongsDatabase.SaveChangesAsyncX();

            versions = versions.Select(v => new Version
            {
                CreatedOn = DateTime.Now,
                Uri = v.Uri,
                User = user,
                Song = songs[v.Song],
            }).ToList();

            SongsDatabase.Versions.AddRange(versions);

            user.Tenant.LatestUsedIndex = 1000;

            await SongsDatabase.SaveChangesAsyncX();

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error initializing the tenant.");
        }
    }
}