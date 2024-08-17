using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Database.Songs.Services;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.Ios;
using Version = OneShelf.Common.Database.Songs.Model.Version;

namespace OneShelf.Telegram.Processor.Services;

public class SimpleActions
{
    private readonly StringsCombiner _stringsCombiner;
    private readonly ILogger<SimpleActions> _logger;
    private readonly Io _io;
    private readonly TelegramOptions _telegramOptions;
    private readonly MessageMarkdownCombiner _messageMarkdownCombiner;
    private readonly ChannelActions _channelActions;
    private readonly FilesUploader _filesUploader;
    private readonly SongsOperations _songsOperations;

    public SimpleActions(StringsCombiner stringsCombiner, ILogger<SimpleActions> logger, Io io, IOptions<TelegramOptions> telegramOptions, MessageMarkdownCombiner messageMarkdownCombiner, ChannelActions channelActions, FilesUploader filesUploader, SongsOperations songsOperations)
    {
        _stringsCombiner = stringsCombiner;
        _logger = logger;
        _io = io;
        _messageMarkdownCombiner = messageMarkdownCombiner;
        _channelActions = channelActions;
        _filesUploader = filesUploader;
        _songsOperations = songsOperations;
        _telegramOptions = telegramOptions.Value;
    }

    public async Task<Task> UploadReadyOnes(bool forceAll = false)
    {
        var toUpload = await _songsOperations.SongsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(x => x.UnparsedTitle == null && x.FileId == null && x.Content.Content != null)
            .Include(x => x.Content)
            .Include(x => x.Artists)
            .OrderBy(x => x.Index)
            .Take(forceAll ? 10000 : 5)
            .ToListAsync();

        async Task Continue()
        {
            foreach (var song in toUpload)
            {
                var fileId = await _filesUploader.Upload(song);
                song.FileId = fileId;
                _io.WriteLine($"uploaded {song.GetCaption()}");

                await _songsOperations.SongsDatabase.SaveChangesAsyncX(true);
            }
        }

        return Continue();
    }

    public async Task ChangeSongIsLive()
    {
        var index = _io.FreeChoiceInt("index: ");

        var song = await _songsOperations.SongsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Include(x => x.Artists)
            .Include(x => x.Likes)
            .Include(x => x.Versions)
            .SingleAsync(x => x.Index == index);

        _io.WriteLine($"artists: {string.Join(", ", song.Artists.Select(x => x.Name))}");
        _io.WriteLine($"title: {song.Title}");
        _io.WriteLine($"SourceUniqueIdentifier: {song.SourceUniqueIdentifier}");
        _io.WriteLine($"CategoryOverride: {song.CategoryOverride}");

        if (song.Versions.Any(x => x.CollectiveType.HasValue))
        {
            throw new("This song is a collective.");
        }

        var newStatus = _io.StrictChoice<SongStatus>("Status:");

        if (song.Status == newStatus)
        {
            return;
        }

        if (song.Status == SongStatus.Live && newStatus != SongStatus.Archived)
        {
            throw new($"{SongStatus.Live} status can only be changed to {SongStatus.Archived}.");
        }

        song.Status = newStatus;

        if (song.Status == SongStatus.Archived)
        {
            foreach (var like in song.Likes)
            {
                song.Likes.Remove(like);
            }
        }

        await _songsOperations.SongsDatabase.SaveChangesAsyncX(true);
    }

    public async Task ChangeSongCategories()
    {
        var index = _io.FreeChoiceInt("index: ");

        var song = await _songsOperations.SongsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Include(x => x.Artists)
            .SingleAsync(x => x.Index == index);

        _io.WriteLine($"artists: {string.Join(", ", song.Artists.Select(x => x.Name))}");
        _io.WriteLine($"title: {song.Title}");
        _io.WriteLine($"SourceUniqueIdentifier: {song.SourceUniqueIdentifier}");
        _io.WriteLine($"CategoryOverride: {song.CategoryOverride}");

        song.CategoryOverride = _io.StrictChoiceNullable<SongCategory>("New category: ");

        await _songsOperations.SongsDatabase.SaveChangesAsyncX();
    }

    public async Task ChangeArtistCategories()
    {
        var name = _io.FreeChoice("artist: ").Trim().ToLowerInvariant();

        var artist = await _songsOperations.SongsDatabase.Artists
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .SingleOrDefaultAsync(o => o.Name == name);

        if (artist == null)
        {
            _io.WriteLine("not found.");
            return;
        }

        _io.WriteLine($"found: {name} -> {artist.CategoryOverride}");

        artist.CategoryOverride = _io.StrictChoiceNullable<SongCategory>("New category: ");

        await _songsOperations.SongsDatabase.SaveChangesAsyncX();
    }

    public async Task SwapArtistAndSynonym()
    {
        var artistTitle = _io.FreeChoice("artist: ").Trim().ToLowerInvariant();

        var artist = await _songsOperations.SongsDatabase.Artists
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(a => a.Name == artistTitle)
            .Include(x => x.Synonyms)
            .SingleOrDefaultAsync();

        if (artist == null)
        {
            _io.WriteLine("not found.");
            return;
        }

        _io.WriteLine($"{artist.Synonyms.Count} synonyms.");
        for (var i = 0; i < artist.Synonyms.Count; i++)
        {
            _io.WriteLine($"{i}. {artist.Synonyms.Skip(i).First().Title}");
        }

        _io.WriteLine();

        var swapWith = _io.FreeChoiceInt("Swap with index:");

        artist.Name = artist.Synonyms.Skip(swapWith).First().Title;
        artist.Synonyms.Skip(swapWith).First().Title = artistTitle;

        await _songsOperations.SongsDatabase.SaveChangesAsyncX(true);
    }

    public async Task RenameArtist()
    {
        var artistTitle = _io.FreeChoice("artist: ").Trim().ToLowerInvariant();

        var artist = await _songsOperations.SongsDatabase.Artists
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(a => a.Name == artistTitle)
            .Include(x => x.Synonyms)
            .SingleOrDefaultAsync();

        if (artist == null)
        {
            _io.WriteLine("not found.");
            return;
        }

        _io.WriteLine($"{artist.Synonyms.Count} synonyms.");
        for (var i = 0; i < artist.Synonyms.Count; i++)
        {
            _io.WriteLine($"{i+1}. {artist.Synonyms.Skip(i).First().Title}");
        }


        _io.WriteLine();

        artist.Name = _io.FreeChoice("New name:").Trim().ToLowerInvariant();

        await _songsOperations.SongsDatabase.SaveChangesAsyncX(true);
    }

    public async Task MergeArtists()
    {
        var artistTitle = _io.FreeChoice("main correct artist: ").Trim().ToLowerInvariant();

        var artist = await _songsOperations.SongsDatabase.Artists
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(a => a.Name == artistTitle)
            .Include(x => x.Synonyms)
            .Include(x => x.Messages)
            .SingleOrDefaultAsync();
        if (artist == null)
        {
            _io.WriteLine("not found.");
            return;
        }

        var artistTitle2 = _io.FreeChoice("duplicate artist: ").Trim().ToLowerInvariant();

        var artist2 = await _songsOperations.SongsDatabase.Artists
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(a => a.Name == artistTitle2)
            .Include(x => x.Synonyms)
            .Include(x => x.Messages)
            .Include(x => x.Songs)
            .SingleOrDefaultAsync();
        if (artist2 == null)
        {
            _io.WriteLine("not found.");
            return;
        }

        if (artist2.Songs.Any())
        {
            _io.WriteLine("Artist 2 has songs. Impossible to merge. First rename those songs to artist 1.");
            return;
        }

        if (artist.CategoryOverride.HasValue && artist2.CategoryOverride.HasValue &&
            artist.CategoryOverride != artist2.CategoryOverride)
        {
            _io.WriteLine($"{artist.Name} CategoryOverride = {artist.CategoryOverride}, {artist2.Name} CategoryOverride = {artist2.CategoryOverride}, impossible to merge.");
            return;
        }

        if (artist2.Messages.Any())
        {
            _io.WriteLine("Artist 2 has messages. Impossible to merge. First run the update.");
            return;
        }

        foreach (var newSynonym in artist2.Synonyms.Select(x => x.Title).Append(artist2.Name).Except(artist.Synonyms.Select(x => x.Title).Append(artist.Name)))
        {
            _io.WriteLine($"Adding new synonym to artist 1: {newSynonym}");
            artist.Synonyms.Add(new()
            {
                Title = newSynonym
            });
        }

        _songsOperations.SongsDatabase.Artists.Remove(artist2);
        await _songsOperations.SongsDatabase.SaveChangesAsyncX(true);
    }

    public async Task UpdateArtistSynonyms()
    {
        var artistTitle = _io.FreeChoice("artist: ").Trim().ToLowerInvariant();

        var artist = await _songsOperations.SongsDatabase.Artists
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(a => a.Name == artistTitle)
            .Include(x => x.Synonyms)
            .SingleOrDefaultAsync();

        if (artist == null)
        {
            _io.WriteLine("not found.");
            return;
        }

        _io.WriteLine($"{artist.Synonyms.Count} synonyms:");
        for (var i = 0; i < artist.Synonyms.Count; i++)
        {
            _io.WriteLine($"{i+1}. {artist.Synonyms.Skip(i).First().Title}");
        }

        var synonyms = new HashSet<string>();

        while (true)
        {
            var synonym = _io.FreeChoice("synonyms, ('-' for break):").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(synonym) || synonym.Trim() == "-")
                break;
            synonyms.Add(synonym);
        }

        artist.Synonyms.Clear();
        foreach (var synonym in synonyms)
        {
            artist.Synonyms.Add(new()
            {
                Title = synonym
            });
        }

        await _songsOperations.SongsDatabase.SaveChangesAsyncX();
    }

    public async Task Rename()
    {
        var index = _io.FreeChoiceInt("index: ");

        var song = await _songsOperations.SongsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Include(x => x.Artists)
            .SingleAsync(x => x.Index == index);

        _io.WriteLine($"artists: {string.Join(", ", song.Artists.Select(x => x.Name))}");
        _io.WriteLine($"title: {song.Title}");
        _io.WriteLine($"SourceUniqueIdentifier: {song.SourceUniqueIdentifier}");

        var artists = new List<Artist>();

        var first = true;
        var intact = false;
        while (true)
        {
            var artist = _io.FreeChoice("artist, ('-' for intact (if first) or break):").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(artist) || artist == "-")
            {
                intact = first;
                break;
            }
            artists.Add(await GetOrCreateArtist(artist));
            first = false;
        }

        if (!intact)
        {
            if (artists.Count == 0) throw new("At least one artist required.");
            song.Artists = artists;
        }

        var title = _io.FreeChoice("title ('-' for intact):").Trim().ToLowerInvariant().SelectSingle(x => x == "-" ? song.Title : x);

        song.Title = title;
        song.UnparsedTitle = null;

        await _songsOperations.SongsDatabase.SaveChangesAsyncX(true);
    }

    public async Task AddSong()
    {
        var artists = new List<Artist>();

        var artist = _io.FreeChoice("artist: ").Trim().ToLowerInvariant();
        artists.Add(await GetOrCreateArtist(artist));

        if (artists.Count == 0) throw new("At least one artist required.");
        
        var title = _io.FreeChoice("title:").Trim().ToLowerInvariant();

        var url = _io.FreeChoice("url ('-' for break):").Trim().ToLowerInvariant();
        url = string.IsNullOrWhiteSpace(url) || url.Trim() == "-" ? null : url;

        var status = _io.StrictChoice<SongStatus>("Status:");
        if (status == SongStatus.Archived)
        {
            _io.WriteLine("Wrong status.");
            return;
        }

        var newIndex = await _songsOperations.SongsDatabase.GetNextSongIndex(_telegramOptions.TenantId);

        var song = new Song
        {
            TenantId = _telegramOptions.TenantId,
            Title = title,
            Artists = artists,
            SourceUniqueIdentifier = Guid.NewGuid().ToString(),
            Versions = url == null
                ? new()
                : new List<Version>
                {
                    new()
                    {
                        Uri = new(url),
                        UserId = _telegramOptions.AdminId,
                    }
                },
            Index = newIndex,
            CreatedByUserId = _telegramOptions.AdminId,
            CreatedOn = DateTime.UtcNow,
            Status = status,
        };

        _songsOperations.SongsDatabase.Songs.Add(song);

        await _songsOperations.SongsDatabase.SaveChangesAsyncX(true);

        _io.WriteLine($"Index = {song.Index}");
    }

    public async Task MergeSongs()
    {
        var index1 = _io.FreeChoiceInt("main song index: ");

        var song1 = await _songsOperations.SongsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Include(x => x.Artists).
            Include(x => x.Versions)
            .Include(x => x.Likes)
            .SingleAsync(x => x.Index == index1);

        _io.WriteLine($"artists: {string.Join(", ", song1.Artists.Select(x => x.Name))}");
        _io.WriteLine($"title: {song1.Title}");
        _io.WriteLine($"additional keywords: {song1.AdditionalKeywords}");

        var index2 = _io.FreeChoiceInt("second song index (to archive): ");

        var song2 = await _songsOperations.SongsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Include(x => x.Artists)
            .Include(x => x.Versions)
            .Include(x => x.Likes)
            .SingleAsync(x => x.Index == index2);

        _io.WriteLine($"artists: {string.Join(", ", song2.Artists.Select(x => x.Name))}");
        _io.WriteLine($"title: {song2.Title}");
        _io.WriteLine($"additional keywords: {song2.AdditionalKeywords}");

        if (song2.Versions.Any(x => x.CollectiveType.HasValue))
        {
            _io.WriteLine("Collectives present. Unable to process.");
            return;
        }

        var yes = _io.StrictChoice<Confirmation>("Are you sure?") == Confirmation.Yes;
        if (!yes)
        {
            _io.WriteLine("Canceled.");
            return;
        }

        foreach (var version in song2.Versions.ToList())
        {
            var song1SameVersions = song1.Versions.Where(x => x.Uri == version.Uri).ToList();
            if (!song1SameVersions.Any())
            {
                var newVersion = new Version
                {
                    SongId = song1.Id,
                    Uri = version.Uri,
                    CreatedOn = version.CreatedOn,
                    UserId = version.UserId,
                    Likes = new List<Like>(),
                };
                _songsOperations.SongsDatabase.Versions.Add(newVersion);
                song1SameVersions.Add(newVersion);
                await _songsOperations.SongsDatabase.SaveChangesAsyncX();
            }

            var song1SameVersion = song1SameVersions.First();

            var matchingLikes = song2.Likes
                .Where(x => x.VersionId == version.Id)
                .Select(x => (
                    like: x,
                    matching: song1.Likes.SingleOrDefault(y =>
                        x.UserId == y.UserId && x.LikeCategoryId == y.LikeCategoryId && y.VersionId == song1SameVersion.Id)))
                .ToList();

            foreach (var (like, matchingLike) in matchingLikes)
            {
                if (matchingLike == null)
                {
                    _songsOperations.SongsDatabase.Likes.Add(new()
                    {
                        SongId = song1.Id,
                        VersionId = song1SameVersion.Id,
                        Level = like.Level,
                        LikeCategoryId = like.LikeCategoryId,
                        CreatedOn = like.CreatedOn,
                        Transpose = like.Transpose,
                        UserId = like.UserId,
                    });
                    _songsOperations.SongsDatabase.Likes.Remove(like);
                }
                else
                {
                    matchingLike.Level = Math.Max(matchingLike.Level, like.Level);
                    _songsOperations.SongsDatabase.Likes.Remove(like);
                }
            }

            await _songsOperations.SongsDatabase.SaveChangesAsyncX();

            _songsOperations.SongsDatabase.Versions.Remove(version);
            await _songsOperations.SongsDatabase.SaveChangesAsyncX();
        }

        var songMatchingLikes = song2.Likes
            .Where(x => x.VersionId == null)
            .Select(x => (
                like: x,
                matching: song1.Likes.SingleOrDefault(y => x.UserId == y.UserId && x.LikeCategoryId == y.LikeCategoryId && y.VersionId == null)))
            .ToList();

        foreach (var (like, matchingLike) in songMatchingLikes)
        {
            if (matchingLike == null)
            {
                _songsOperations.SongsDatabase.Likes.Add(new()
                {
                    SongId = song1.Id,
                    Level = like.Level,
                    LikeCategoryId = like.LikeCategoryId,
                    CreatedOn = like.CreatedOn,
                    Transpose = like.Transpose,
                    UserId = like.UserId,
                });
                _songsOperations.SongsDatabase.Likes.Remove(like);
            }
            else
            {
                matchingLike.Level = Math.Max(matchingLike.Level, like.Level);
                _songsOperations.SongsDatabase.Likes.Remove(like);
            }
        }

        song2.Status = SongStatus.Archived;

        await _songsOperations.SongsDatabase.SaveChangesAsyncX();

        _io.WriteLine("Done.");
    }

    private enum ChangeSongVersionMode
    {
        Add,
        Update,
        Delete,
    }

    public async Task ChangeSongVersion()
    {
        var index = _io.FreeChoiceInt("index: ");

        var song = await _songsOperations.SongsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Include(x => x.Artists)
            .Include(x => x.Versions)
            .SingleAsync(x => x.Index == index);

        _io.WriteLine($"artists: {string.Join(", ", song.Artists.Select(x => x.Name))}");
        _io.WriteLine($"title: {song.Title}");
        _io.WriteLine($"SourceUniqueIdentifier: {song.SourceUniqueIdentifier}");
        _io.WriteLine($"AdditionalKeywords: {song.AdditionalKeywords}");

        foreach (var (x, i) in song.Versions.OrderBy(x => x.Id).WithIndices())
        {
            _io.WriteLine($"{i}. Url: {x.Uri}");
        }

        var mode = !song.Versions.Any() 
            ? ChangeSongVersionMode.Add 
            : _io.StrictChoice<ChangeSongVersionMode>("Type of action?");

        _io.WriteLine($"{nameof(mode)} = {mode}");

        var versionIndex = 0;
        if (song.Versions.Count > 1 && mode != ChangeSongVersionMode.Add)
        {
            versionIndex = _io.FreeChoiceInt("Execute the action with index:");
        }

        var existingVersion = song.Versions.OrderBy(x => x.Id).Skip(versionIndex).FirstOrDefault();
        if (mode == ChangeSongVersionMode.Delete)
        {
            if (existingVersion!.PublishedSettings != null || existingVersion.CollectiveType.HasValue)
            {
                _io.WriteLine("Unable to delete this version, it is published or is a collective.");
                return;
            }

            song.Versions.Remove(existingVersion);
            await _songsOperations.SongsDatabase.SaveChangesAsyncX(true);
            return;
        }

        var version = _io.FreeChoice("url ('-' to retain):").Trim().ToLowerInvariant();
        version = string.IsNullOrWhiteSpace(version) || version.Trim() == "-" ? null : version;

        if (mode == ChangeSongVersionMode.Add)
        {
            if (version == null)
            {
                _io.WriteLine("Adding a null version doesn't make sense.");
                return;
            }

            song.Versions.Add(new()
            {
                Uri = new(version),
                UserId = _telegramOptions.AdminId,
            });

            await _songsOperations.SongsDatabase.SaveChangesAsyncX(true);
            return;
        }

        if (existingVersion!.CollectiveType.HasValue)
        {
            _io.WriteLine("Unable to update this version, it is a collective.");
            return;
        }

        // update mode, at least one version exists, urlIndex is correct
        existingVersion.Uri = new(version ?? existingVersion.Uri.ToString());

        await _songsOperations.SongsDatabase.SaveChangesAsyncX(true);
    }

    public async Task AdditionalKeywords()
    {
        var index = _io.FreeChoiceInt("index: ");

        var song = await _songsOperations.SongsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Include(x => x.Artists)
            .SingleAsync(x => x.Index == index);
        _io.WriteLine($"artists: {string.Join(", ", song.Artists.Select(x => x.Name))}");

        _io.WriteLine($"title: {song.Title}");
        _io.WriteLine($"SourceUniqueIdentifier: {song.SourceUniqueIdentifier}");
        _io.WriteLine($"AdditionalKeywords: {song.AdditionalKeywords}");

        var entry = _io.FreeChoice("additional keywords ('-' for null):").Trim().ToLowerInvariant();

        song.AdditionalKeywords = string.IsNullOrWhiteSpace(entry) || entry.Trim() == "-" ? null : entry;
        _io.WriteLine($"{nameof(song.AdditionalKeywords)} := {song.AdditionalKeywords ?? "null"}");

        await _songsOperations.SongsDatabase.SaveChangesAsyncX(true);
    }

    public async Task ListMultiArtistSongs()
    {
        var songs = await _songsOperations.SongsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(x => x.Artists.Count() > 1)
            .Include(x => x.Artists)
            .ToListAsync();
        foreach (var song in songs)
        {
            _io.WriteLine($"{song.Index}: {song.Title}: {string.Join(", ", song.Artists.Select(x => x.Name))}");
        }
    }

    public async Task ListArchived()
    {
        await List(SongStatus.Archived);
    }

    public async Task ListDraft()
    {
        await List(SongStatus.Draft);
    }

    public async Task ListPostponed()
    {
        await List(SongStatus.Postponed);
    }

    public async Task ListLiveNoChords()
    {
        await List(SongStatus.Live, true);
    }

    private async Task List(SongStatus songStatus, bool onlyNoChords = false)
    {
        var songs = await _songsOperations.SongsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(x => x.Status == songStatus)
            .SelectSingle(x => onlyNoChords ? x.Where(x => x.Content == null && !x.Versions.Any()) : x)
            .Include(x => x.Artists)
            .Select(x => new
            {
                Song = x,
                HasFile = x.Content != null,
                HasVersion = x.Versions.Any()
            })
            .OrderBy(x => x.Song.Index)
            .ToListAsync();
        _io.WriteLine(songStatus.ToString());
        _io.WriteLine();
        foreach (var song in songs)
        {
            _io.WriteLine($"{song.Song.GetCaption()} ({string.Join(", ", new[] { (song.HasFile ? "File" : null), (song.HasVersion ? "Version" : null), !song.HasFile && !song.HasVersion ? "None" : null }.Where(x => x != null))})");
        }
    }

    public async Task ListSongsWithAdditionalKeywordsOrComments()
    {
        var songs = await _songsOperations.SongsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Include(x => x.Artists)
            .Where(s => s.AdditionalKeywords != null)
            .ToListAsync();

        foreach (var song in songs)
        {
            _io.WriteLine(
                $"{song.Index} ({song.Status}): {string.Join(", ", song.Artists.Select(x => x.Name))} - {song.Title} ({song.AdditionalKeywords})");
        }
    }

    private async Task<Artist> GetOrCreateArtist(string name)
    {
        var artist = await _songsOperations.SongsDatabase.Artists
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .SingleOrDefaultAsync(x => x.Name == name);

        var synonym = await _songsOperations.SongsDatabase.ArtistSynonyms
            .Where(x => x.Artist.TenantId == _telegramOptions.TenantId)
            .Include(x => x.Artist)
            .SingleOrDefaultAsync(x => x.Title == name);

        if (artist != null) return artist;
        if (synonym?.Artist != null) return synonym.Artist;

        artist = new()
        {
            TenantId = _telegramOptions.TenantId,
            Name = name
        };

        _songsOperations.SongsDatabase.Artists.Add(artist);
        await _songsOperations.SongsDatabase.SaveChangesAsyncX();

        return artist;
    }

    public async Task MeasureAll()
    {
        await Measure(
            MessageType.Song,
            _messageMarkdownCombiner.Songs);

        await Measure(
            MessageType.Artist,
            _messageMarkdownCombiner.Artists);

        await Measure(
            MessageType.CategoryPart,
            _messageMarkdownCombiner.CategoryParts);

        await Measure(
            MessageType.Main,
            _messageMarkdownCombiner.Main);
    }

    private async Task Measure<T>(MessageType messageType, Func<Task<T>> function)
    {
        var started = DateTime.Now;
        await function();
        var milliseconds = (int)(DateTime.Now - started).TotalMilliseconds;
        _io.WriteLine($"{messageType}: {milliseconds} ms.");
    }
}