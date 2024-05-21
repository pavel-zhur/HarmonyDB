using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Songs.FullTextSearch;
using OneShelf.Telegram.Processor.Model;
using Telegram.BotAPI.InlineMode;

namespace OneShelf.Telegram.Processor.Services;

public class FullTextSearch
{
    private readonly ILogger<FullTextSearch> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TelegramOptions _options;

    private (IReadOnlyCollection<(Song song, IReadOnlyCollection<string> words)> songs, int version)? _readCache;
    private readonly Dictionary<int, InlineQueryResultArticle> _articlesCache = new();
    private readonly object _readCacheLockObject = new();

    public FullTextSearch(ILogger<FullTextSearch> logger, IServiceScopeFactory serviceScopeFactory, IOptions<TelegramOptions> options)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
    }

    public async Task<(List<Song> found, bool isUserSpecific, int version)> Find(string query, long userId)
    {
        var (songs, version) = await Read();

        var (list, isUserSpecific) = FullTextSearchLogic.Find(songs, query, userId);

        return (list, isUserSpecific, version);
    }

    private async Task<(IReadOnlyCollection<(Song song, IReadOnlyCollection<string> words)> songs, int version)> Read()
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var songsDatabase = scope.ServiceProvider.GetRequiredService<SongsDatabase>();

        lock (_readCacheLockObject)
        {
            if (_readCache?.version == songsDatabase.GetVersion())
            {
                return _readCache.Value;
            }
        }

        var songs = await songsDatabase.Songs
            .Where(x => x.TenantId == _options.TenantId)
            .Where(x => x.Status == SongStatus.Live && x.Messages.Any(x => x.Type == MessageType.Song))
            .Include(x => x.Likes)
            .Include(x => x.Artists)
            .ThenInclude(x => x.Synonyms)
            .Include(x => x.Versions)
            .ThenInclude(x => x.User)
            .ToListAsync();

        lock (_readCacheLockObject)
        {
            if (!(_readCache?.version > songsDatabase.GetVersion()))
            {
                _articlesCache.Clear();

                _readCache = (FullTextSearchLogic.BuildCache(songs, x => x.Artists), songsDatabase.GetVersion());
            }

            return _readCache.Value;
        }
    }

    public InlineQueryResultArticle GetArticle(Song song, int version, Func<InlineQueryResultArticle> absentCache)
    {
        lock (_readCacheLockObject)
        {
            if (_articlesCache.TryGetValue(song.Id, out var value)) return value;

            var result = absentCache();
            if (version != _readCache?.version) return result;

            return _articlesCache[song.Id] = result;
        }
    }
}