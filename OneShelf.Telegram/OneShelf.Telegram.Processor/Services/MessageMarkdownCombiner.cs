using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Helpers;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Database.Songs.Services;
using OneShelf.Common.Songs;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.InlineMode;
using TelegramOptions = OneShelf.Telegram.Processor.Model.TelegramOptions;

namespace OneShelf.Telegram.Processor.Services;

public class MessageMarkdownCombiner
{
    private readonly ILogger<MessageMarkdownCombiner> _logger;
    private readonly SongsDatabase _songsDatabase;
    private readonly CategoriesCatalog _categoriesCatalog;
    private readonly StringsCombiner _stringsCombiner;
    private readonly TelegramOptions _telegramOptions;

    public MessageMarkdownCombiner(ILogger<MessageMarkdownCombiner> logger, SongsDatabase songsDatabase, CategoriesCatalog categoriesCatalog, StringsCombiner stringsCombiner, IOptions<TelegramOptions> telegramOptions)
    {
        _logger = logger;
        _songsDatabase = songsDatabase;
        _categoriesCatalog = categoriesCatalog;
        _stringsCombiner = stringsCombiner;
        _telegramOptions = telegramOptions.Value;
    }

    public async Task<MessageMarkup> Main()
    {
        var types = new[] { MessageType.CategoryPart, };

        var messages = await _songsDatabase.Messages
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(x => types.Contains(x.Type))
            .OrderBy(x => x.Id)
            .ToListAsync();
        
        var categories = messages
            .Where(x => x.Type == MessageType.CategoryPart)
            .ToLookup(x => x.Category!.Value);

        var markup = new Markdown();
        markup.AppendLine("Новое:".Bold());

        foreach (var (category, title) in _categoriesCatalog.All.OrderBy(x => x.categoryTitle))
        {
            markup.AppendLine();
            foreach (var list in categories[category].OrderBy(x => x.Part))
            {
                markup.Append($"{Constants.IconList} ");
                markup.AppendLine(_stringsCombiner.BuildUrl(
                    title.GetCategoryTitle(list.Part),
                    list.MessageId));
            }
        }

        return new()
        {
            BodyOrCaption = markup,
            Pin = true,
        };
    }

    private Markdown ListHearts(IEnumerable<Like> likes)
    {
        var counts = likes.Where(x => x.Level > 0).GroupBy(x => x.UserId).Select(x => (userId: x.Key, level: x.Max(x => x.Level))).GroupBy(x => x.level).ToDictionary(x => x.Key, x => x.Count());

        return ListHearts(
            counts.TryGetValue(1, out var count) ? count : 0,
            counts.TryGetValue(2, out count) ? count : 0,
            counts.TryGetValue(3, out count) ? count : 0);
    }

    private Markdown ListHearts(int count1, int count2, int count3)
    {
        return string
            .Join(
                string.Empty,
                Enumerable.Repeat(SongsConstants.HeartsByLevel[1], count1)
                    .Concat(Enumerable.Repeat(SongsConstants.HeartsByLevel[2], count2))
                    .Concat(Enumerable.Repeat(SongsConstants.HeartsByLevel[3], count3)))
            .ToMarkdown();
    }

    public async Task<List<(long userId, MessageMarkup message, string part, string title)>> Shortlists(long onlyUserId)
    {
        var likes = await _songsDatabase.Likes
            .Where(x => x.Song.TenantId == _telegramOptions.TenantId)
            .Where(x => x.User.TenantId == _telegramOptions.TenantId)
            .Where(x => x.Level > 0)
            .Where(x => x.UserId == onlyUserId)
            .Include(x => x.User)
            .Include(x => x.Song)
            .ThenInclude(x => x.Artists)
            .Include(x => x.Song)
            .ThenInclude(x => x.Likes)
            .Include(x => x.Song)
            .ThenInclude(x => x.Versions)
            .Where(x => x.Song.Messages.Any(x => x.Type == MessageType.Song))
            .Select(like => new
            {
                like,
                messageId = like.Song.Messages.Single(x => x.Type == MessageType.Song).MessageId
            })
            .ToListAsync();

        var users = likes
            .Where(x => x.like.Level > 0)
            .GroupBy(x => x.like.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        List<(long userId, MessageMarkup message, string part, string title)> result = new();
        foreach (var (userId, userLikes) in users)
        {
            var parts = userLikes
                .Where(x => x.like.Level > 0)
                .GroupBy(x => x.like.SongId)
                .Select(g => g.OrderByDescending(x => x.like.Level).First())
                .OrderByDescending(x => x.like.Level)
                .ThenBy(x => x.like.Song.Likes.GetRating())
                .WithIndices()
                .GroupBy(x => x.i / Constants.ShortlistsPerPart, x => x.x)
                .Select(g => (part: g.Key, partLikes: g.Where(x => x.like.Level > 0).ToList()))
                .OrderBy(g => g.part)
                .ToList();

            foreach (var (part, partLikes) in parts)
            {
                var markup = new Markdown();
                var userTitle = userLikes.First().like.User.Title;
                markup.Append("Шортлист ");
                markup.Append(userId.BuildUrl(userTitle));
                if (parts.Count > 1)
                {
                    markup.Append($" ({part * Constants.ShortlistsPerPart + 1}..{part * Constants.ShortlistsPerPart + partLikes.Count} / {userLikes.Count})");
                }

                markup.Append(":");
                markup = markup.Bold();

                markup.AppendLine();

                foreach (var level in partLikes
                             .GroupBy(x => x.like.Level)
                             .OrderByDescending(x => x.Key))
                {
                    foreach (var (like, caption) in level
                                 .Select(x => (like: x, caption: x.like.Song.GetCaption(false)))
                                 .OrderBy(x => x.caption))
                    {
                        markup.Append(_stringsCombiner.BuildUrl($"{string.Join("", SongsConstants.HeartsByLevel[like.like.Level])} {caption}", like.messageId));

                        if (like.like.Song.FileId == null && !like.like.Song.Versions.Any())
                        {
                            markup.Append($" {Constants.IconLook}");
                        }

                        var otherLikes = like.like.Song.Likes.Where(x => x.Level > 0).Where(x => x != like.like).ToList();
                        if (otherLikes.Any())
                        {
                            markup.Append(" ");
                            markup.Append(ListHearts(otherLikes));
                        }

                        markup.AppendLine();
                    }

                    markup.AppendLine();
                }

                result.Add((userId, new()
                {
                    BodyOrCaption = markup,
                }, part.ToString(), $"{userTitle}-{part}"));
            }
        }

        return result;
    }

    public async Task<List<(SongCategory category, string? part, MessageMarkup message)>> CategoryParts()
    {
        var songMessages = await _songsDatabase.Messages
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(x => x.Type == MessageType.Song)
            .Include(x => x.Song)
            .ThenInclude(x => x.Artists)
            .ThenInclude(x => x.Synonyms)
            .Include(x => x.Song)
            .ThenInclude(x => x.Likes)
            .Include(x => x.Song)
            .ThenInclude(x => x.Versions)
            .ToListAsync();
        
        var byArtists = songMessages
            .Select(m => m.Song)
            .SelectMany(s => s.Artists.SelectMany(a =>
            {
                var artistCategory = a.GetSongCategory();
                return new[] { (a, artistTitle: a.Name, artistCategory) }
                        .Concat(a.Synonyms.Select(x => (a, artistTitle: x.Title, artistCategory)))
                        .Select(at => (s, at.a, at.artistCategory, at.artistTitle));
            }))
            .GroupBy(s =>
            {
                var category = s.s.CategoryOverride ?? s.artistCategory;
                var part = _categoriesCatalog.GetPart(category, s.artistTitle);
                return (s.artistTitle, s.a.Id, category, part);
            })
            .ToDictionary(x => x.Key, x => x.Select(y => y.s).ToList());

        var artistMessageIds = await _songsDatabase.Messages
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(x => x.Type == MessageType.Artist)
            .ToDictionaryAsync(x => x.ArtistId.Value, x => x.MessageId);

        List<(SongCategory category, string? part, MessageMarkup message)> result = new();
        foreach (var (category, categoryTitle) in _categoriesCatalog.All.OrderBy(x => x.categoryTitle))
        {
            foreach (var part in byArtists
                         .Where(a => a.Key.category == category)
                         .GroupBy(a => a.Key.part)
                         .OrderBy(g => g.Key))
            {
                var markup = new Markdown();
                markup.AppendLine(categoryTitle.GetCategoryTitle(part.Key).Bold());
                markup.AppendLine();

                char? previous = null;
                foreach (var ((artist, artistId, _, _), artistSongs) in part.OrderBy(a => a.Key.artistTitle))
                {
                    int? artistMessageId = artistMessageIds.TryGetValue(artistId, out var value) ? value : null;

                    previous ??= artist[0];
                    if (previous != artist[0])
                    {
                        markup.AppendLine();
                        previous = artist[0];
                    }

                    markup.Append($"{Constants.IconStar} ");

                    var likes = artistSongs.Sum(x => x.Likes.Where(x => x.Level > 0).Select(x => x.UserId).Distinct().Count());

                    switch (artistSongs.Count)
                    {
                        case 1:
                            var song = artistSongs.Single();
                            markup.Append(artist.Bold());
                            markup.Append(" - ");
                            markup.Append(
                                _stringsCombiner.BuildUrl($"{song.Title}{(song.AdditionalKeywords != null ? $" ({song.AdditionalKeywords})" : null)}", song.Messages.Single(x => x.Type == MessageType.Song).MessageId));

                            if (song.FileId == null && !song.Versions.Any())
                            {
                                markup.Append($" {Constants.IconLook}");
                            }

                            if (likes > 0)
                            {
                                markup.Append($" {Constants.IconGreenHeart}{Constants.IconTimes}{likes}");
                            }

                            markup.AppendLine();
                            break;
                        default:
                            markup.Append(artist.Bold());
                            markup.Append(" - ");
                            markup.Append(_stringsCombiner.BuildUrl(
                                $"{Constants.IconList} {artistSongs.Count} {artistSongs.Count.SongsPluralWord()}",
                                artistMessageId!.Value));

                            if (likes > 0)
                            {
                                markup.Append($" {Constants.IconGreenHeart}{Constants.IconTimes}{likes}");
                            }

                            markup.AppendLine();
                            break;
                    }
                }

                result.Add((category, part.Key, new()
                {
                    BodyOrCaption = markup,
                }));
            }
        }

        return result;
    }

    public async Task<List<(int artistId, MessageMarkup message, string title)>> Artists()
    {
        var songMessages = await _songsDatabase.Messages
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(x => x.Type == MessageType.Song)
            .Include(x => x.Song)
            .ThenInclude(x => x.Artists)
            .Include(x => x.Song)
            .ThenInclude(x => x.Likes)
            .Include(x => x.Song)
            .ThenInclude(x => x.Versions)
            .ToListAsync();

        var artists = songMessages
            .Select(m => m.Song)
            .SelectMany(s => s.Artists.Select(a => (s, a)))
            .GroupBy(s => s.a)
            .OrderBy(_ => Random.Shared.NextDouble())
            .ToDictionary(x => x.Key.Id, x => x.Select(y => y.s).OrderBy(s => s.Title).ToList());

        List<(int artistId, MessageMarkup message, string title)> result = new();
        foreach (var (artistId, artistSongs) in artists)
        {
            if (artistSongs.Count == 1) continue;

            var artist = artistSongs[0].Artists.Single(x => x.Id == artistId);

            var markup = new Markdown();
            markup.AppendLine(artist.Name.Bold());
            markup.AppendLine();
            foreach (var song in artistSongs)
            {
                markup.Append("- ");
                markup.Append(_stringsCombiner.BuildUrl(song.Title, song.Messages.Single(x => x.Type == MessageType.Song).MessageId));
                if (song.AdditionalKeywords != null)
                {
                    markup.Append($" ({song.AdditionalKeywords})");
                }

                if (song.FileId == null && !song.Versions.Any())
                {
                    markup.Append($" {Constants.IconLook}".Bold());
                }

                if (song.Likes.Any(x => x.Level > 0))
                {
                    var hearts = ListHearts(song.Likes);
                    markup.Append(" ");
                    markup.Append(hearts);
                }

                markup.AppendLine();
            }

            result.Add((artistId, new()
            {
                BodyOrCaption = markup,
            }, artist.Name));
        }
        
        return result;
    }

    public async Task<List<(int songId, MessageMarkup message, string caption)>> Songs()
    {
        var artists = await _songsDatabase.Artists
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Select(a => new
            {
                a.Id,
                Count = a.Songs.Count(s => s.Messages.Any(x => x.Type == MessageType.Song)),
                MessageId = (int?)a.Messages.FirstOrDefault().MessageId,
            })
            .Where(a => a.MessageId.HasValue && a.Count > 1)
            .ToDictionaryAsync(x => x.Id);

        var songs = await _songsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(x => x.UnparsedTitle == null)
            .Where(x => x.FileId != null || x.Content == null)
            .Where(x => x.Status == SongStatus.Live)
            .Include(x => x.RedirectsFromSongs)
            .Include(x => x.Artists)
            .Include(x => x.Versions)
            .Select(s => new
            {
                Song = s,
                MessageId = (int?)s.Messages.SingleOrDefault(x => x.Type == MessageType.Song)!.MessageId,
            })
            .ToListAsync();

        var likes = (await _songsDatabase.Likes
            .Where(x => x.User.TenantId == _telegramOptions.TenantId)
            .Where(x => x.Song.TenantId == _telegramOptions.TenantId)
            .Where(x => x.Level > 0)
            .Include(x => x.User)
            .ToListAsync()).ToLookup(x => x.SongId);

        var songsByGroup = songs
            .Where(x => x.Song.SameSongGroupId.HasValue)
            .Where(x => x.MessageId.HasValue)
            .ToLookup(x => x.Song.SameSongGroupId.Value);

        var result = new List<(int songId, MessageMarkup message, string caption)>();
        foreach (var (song, messageId) in songs.OrderBy(x => x.Song.Index).Select(x => (x.Song, x.MessageId)))
        {
            var markup = new Markdown();
            markup.Append($"{song.Index:000}. ");
            markup.Append(string.Join(", ", song.Artists.Select(x => x.Name).OrderBy(x => x)).Bold());
            markup.Append(" - ");
            markup.Append(song.Title);
            if (!string.IsNullOrWhiteSpace(song.AdditionalKeywords))
            {
                markup.Append($" ({song.AdditionalKeywords})");
            }

            foreach (var artist in song.Artists.Select(a => artists.TryGetValue(a.Id, out var x) ? (a, x) : default).Where(x => x.a != null))
            {
                markup.AppendLine();
                if (artist.x.Count < 4)
                {
                    markup += _stringsCombiner.BuildUrl(
                        $"{Constants.IconList} еще {artist.x.Count - 1} {(artist.x.Count - 1).SongsPluralWord()} {artist.a.Name}",
                        artist.x.MessageId!.Value);
                }
                else
                {
                    markup += _stringsCombiner.BuildUrl(
                        $"{Constants.IconList} еще много песен {artist.a.Name}",
                        artist.x.MessageId!.Value);
                }
            }

            if (song.Versions.Any())
            {
                markup.AppendLine();
                foreach (var version in song.Versions.OrderBy(x => x.Id))
                {
                    markup.AppendLine();
                    markup += version.Uri.BuildUrl($"Ссылка на аккорды {version.Uri.Host} {Constants.IconLink}");
                }
            }
            else if (song.FileId == null)
            {
                markup.AppendLine();
                markup.AppendLine();
                markup += $"Я ищу свои аккорды {Constants.IconLook}";
            }

            if (likes[song.Id].Any())
            {
                markup.AppendLine();
                markup.AppendLine();
                markup += Markdown.Join(
                    Environment.NewLine, 
                    likes[song.Id]
                        .GroupBy(x => x.UserId)
                        .Select(g => (user: g.First().User, level: g.Max(x => x.Level), createdOn: g.Min(x => x.CreatedOn)))
                        .OrderBy(x => x.createdOn)
                        .Select(x => $"{x.user.Title} {SongsConstants.HeartsByLevel[x.level]}".ToMarkdown()));
            }

            if (song.RedirectsFromSongs.Any())
            {
                markup.AppendLine();
                markup.AppendLine();
                var caption = song.RedirectsFromSongs.Count == 1 ? "Устаревший индекс" : "Устаревшие индексы";
                markup += $"{caption}: {string.Join(", ", song.RedirectsFromSongs.Select(x => $"{x.Index:000}"))}";
            }

            if (song.SameSongGroupId.HasValue)
            {
                var same = songsByGroup[song.SameSongGroupId.Value].Where(x => x.MessageId.HasValue && x.Song != song).ToList();
                if (same.Any())
                {
                    markup.AppendLine();
                    markup.AppendLine();
                    markup += Markdown.Join(
                        Environment.NewLine,
                        same.Select(x => "Другой вариант: " + _stringsCombiner.BuildUrl(x.Song.GetCaption(false), x.MessageId.Value)));
                }
            }

            result.Add((song.Id, new()
            {
                BodyOrCaption = markup,
                FileId = song.FileId,
                InlineKeyboardMarkup = new(
                    [
                        [
                            new(Constants.IconCross) { CallbackData = Constants.HeartsCallbacks[0] },
                            new(SongsConstants.IconYellowHeart) { CallbackData = Constants.HeartsCallbacks[1] },
                            new(SongsConstants.IconOrangeHeart) { CallbackData = Constants.HeartsCallbacks[2] },
                            new(SongsConstants.IconRedHeart) { CallbackData = Constants.HeartsCallbacks[3] },
                        ]
                    ])
            }, song.GetCaption()));
        }

        return result;
    }

    public async Task<Markdown> SearchResult(IReadOnlyCollection<int> songIds)
    {
        var idsToOrder = songIds.WithIndices().ToDictionary(x => x.x, x => x.i);

        var songs = await _songsDatabase.Songs
            .Where(x => x.TenantId == _telegramOptions.TenantId)
            .Where(x => songIds.Contains(x.Id) && x.Status == SongStatus.Live)
            .Include(x => x.Artists)
            .Include(x => x.Messages)
            .Include(x => x.Likes)
            .Include(x => x.Versions)
            .ThenInclude(x => x.User)
            .OrderBy(x => x.CreatedOn)
            .ToListAsync();

        var markup = new Markdown();

        if (!songs.Any())
        {
            markup.AppendLine("Не нашлось ни одной песни.");
        }

        foreach (var song in songs.OrderBy(x => idsToOrder[x.Id]))
        {
            AppendSongLines(markup, song, false);
            
            markup.AppendLine();
        }

        return markup;
    }

    private void AppendSongLines(Markdown builder, Song songWithArtistsMessagesLikesVersionsAndVersionUsers, bool outdated)
    {
        var song = songWithArtistsMessagesLikesVersionsAndVersionUsers;

        builder.Append($"{song.Index}. ");

        builder.Append(
            string.Join(", ", song.Artists.Select(x => x.Name)).Bold());
        builder.Append(" - ");

        builder.Append(_stringsCombiner.BuildUrl(
            song.Title.Bold(),
            song.Messages.Single(x => x.Type == MessageType.Song).MessageId,
            false));

        if (song.AdditionalKeywords != null)
        {
            builder.Append($" ({song.AdditionalKeywords})");
        }

        if (!outdated)
        {
            builder.Append(" ");
            builder.Append(ListHearts(song.Likes));
        }

        builder.AppendLine();

        if (!outdated)
        {
            AppendSongChordsLines(builder, song, true);
        }
    }

    public InlineQueryResultArticle Article(Song song)
    {
        var title = $"{song.Index}. {string.Join(", ", song.Artists.Select(x => x.Name))} - {song.Title}";

        StringBuilder description = new();
        if (song.Versions.Any() || song.FileId != null)
        {
            description.AppendLine(
                string.Join(
                    ", ", 
                    song.Versions
                        .OrderBy(x => x.Id)
                        .Select(x => x.Uri.Host)
                        .Distinct()
                        .Select(x => $"{x} {Constants.IconLink}")
                        .SelectSingle(x => song.FileId == null ? x : x.Append("pdf из книжек"))));
        }
        else
        {
            description.AppendLine($"{Constants.IconLook} ищу свои аккорды.");
        }

        if (song.AdditionalKeywords != null)
        {
            description.AppendLine(song.AdditionalKeywords);
        }

        Markdown message = new();
        message.Append($"{song.Index}. ");
        message.Append(string.Join(", ", song.Artists.Select(x => x.Name)).Bold());
        message.Append($" - {song.Title}{(song.AdditionalKeywords != null ? $" ({song.AdditionalKeywords})" : null)}");
        message.AppendLine();
        message.AppendLine();
        AppendSongChordsLines(message, song, false);

        return new()
        {
            Title = title,
            Id = song.Index.ToString(),
            InputMessageContent = new InputTextMessageContent(message.ToString())
            {
                ParseMode = Constants.MarkdownV2,
                LinkPreviewOptions = new()
                {
                    IsDisabled = true,
                },
            },
            Description = description.ToString(),
        };
    }

    public void AppendSongChordsLines(Markdown markdown, Song songWithVersionsAndVersionUsersAndProbablyMessages, bool includeMainPdfLink, bool includeManageVersion = true)
    {
        var song = songWithVersionsAndVersionUsersAndProbablyMessages;

        var versions = song.Versions.Where(x => x.CollectiveType is null or VersionCollectiveType.Public).ToList();

        if (song.FileId == null && !versions.Any())
        {
            markdown.Append($"{Constants.IconLook} Аккордов пока нет");
            if (includeManageVersion)
            {
                markdown.Append(", ");
                markdown.AppendLine(_stringsCombiner.BuildManagementUrl($"{Constants.IconImprove}добавить.", song.Index));
            }
            else
            {
                markdown.AppendLine();
            }
        }
        else
        {
            if (song.FileId != null && includeMainPdfLink)
            {
                markdown.AppendLine(_stringsCombiner.BuildUrl(
                    $"{Constants.IconLink} Файлик с аккордами (офлайн, книжная версия)",
                    song.Messages.Single(x => x.Type == MessageType.Song).MessageId));
            }

            foreach (var version in versions.OrderBy(x => x.Id))
            {
                markdown.Append(version.Uri.BuildUrl($"{Constants.IconLink} Аккорды {version.Uri.Host}"));
                if (version.UserId != _telegramOptions.AdminId)
                {
                    markdown.Append(" (");
                    if (version.UserId != _telegramOptions.AdminId)
                    {
                        markdown.Append(version.UserId.BuildUrl(version.User.Title));
                    }

                    markdown.Append(")");
                }

                markdown.AppendLine();
            }
        }
    }
}