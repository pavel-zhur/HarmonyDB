using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Services.PipelineHandlers.Base;
using OneShelf.Telegram.Services;
using Telegram.BotAPI.AvailableMethods;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class LikesHandler : CallbackQueryHandler
{
    private readonly ILogger<LikesHandler> _logger;
    private readonly RegenerationQueue _regenerationQueue;
    private readonly IoFactory _ioFactory;

    private Song _song = null!;
    private IReadOnlyList<Like> _likes = null!;
    private bool _need;

    public LikesHandler(IOptions<TelegramOptions> telegramOptions, ILogger<LikesHandler> logger, SongsDatabase songsDatabase, RegenerationQueue regenerationQueue, IoFactory ioFactory) 
        : base(telegramOptions, songsDatabase)
    {
        _logger = logger;
        _regenerationQueue = regenerationQueue;
        _ioFactory = ioFactory;
    }

    protected override IReadOnlyList<string> Catch => Constants.HeartsCallbacks;

    protected override async Task Handle(AnswerCallbackQueryArgs answerCallbackQueryArgs)
    {
        var song = await SongsDatabase.Songs
            .Where(x => x.TenantId == TelegramOptions.TenantId)
            .Include(x => x.Artists)
            .SingleAsync(x => x.Id == SongId);

        var existingLikes = await SongsDatabase.Likes
            .Where(x => x.Song.TenantId == TelegramOptions.TenantId)
            .Where(x => x.User.TenantId == TelegramOptions.TenantId)
            .Where(x => x.SongId == SongId && x.UserId == UserId)
            .ToListAsync();

        _song = song;
        _likes = existingLikes;

        _need = CatchIndex > 0;

        answerCallbackQueryArgs.Text = $"{(_need ? "Лайк" : "Отмена лайка")} {_song.GetCaption(false)} {(_need ? "записан. Скоро появится" : "записана. Скоро исчезнет")}.";
        answerCallbackQueryArgs.ShowAlert = true;
    }

    protected override async Task PostHandle()
    {
        await base.PostHandle();

        var level = CatchIndex;

        if (!_need)
        {
            if (!_likes.Any())
            {
                LogInformation("overdislikes");
                return;
            }

            foreach (var like in _likes)
            {
                SongsDatabase.Likes.Entry(like).State = EntityState.Deleted;
            }

            await SongsDatabase.SaveChangesAsyncX();
            
            LogInformation("dislikes");
        }
        else if (_likes.Count == 0)
        {
            var like = new Like
            {
                UserId = UserId,
                SongId = SongId,
                Level = level,
                CreatedOn = DateTime.Now,
            };

            SongsDatabase.Likes.Add(like);
            await SongsDatabase.SaveChangesAsyncX();

            LogInformation("likes");
        }
        else if (_likes.Count == 1)
        {
            if (_likes.Single().Level == level)
            {
                LogInformation("overlikes");
                return;
            }

            _likes.Single().Level = level;
            await SongsDatabase.SaveChangesAsyncX();

            LogInformation("likes");
        }
        else if (_likes.Select(x => x.Level).Distinct().Count() == 1)
        {
            if (_likes.First().Level == level)
            {
                LogInformation("overlikes");
                return;
            }

            foreach (var like in _likes)
            {
                like.Level = level;
            }
            
            await SongsDatabase.SaveChangesAsyncX();

            LogInformation("likes");
        }
        else if (_likes.Max(x => x.Level) > level) // limiting
        {
            foreach (var like in _likes)
            {
                like.Level = Math.Min(like.Level, level);
            }

            await SongsDatabase.SaveChangesAsyncX();

            LogInformation("likes");
        }
        else if (_likes.Max(x => x.Level) == level) // do nothing
        {
        }
        else if (_likes.Max(x => x.Level) < level) // increasing
        {
            var like = _likes.OrderBy(x => x.VersionId.HasValue ? 2 : 1).ThenByDescending(x => x.Level).First(); // song goes first, otherwise the increasing the biggest
            like.Level = level;

            await SongsDatabase.SaveChangesAsyncX();

            LogInformation("likes");
        }

        _ioFactory.InitSilence(UserId, null);
        _regenerationQueue.QueueUpdateAll(false);
    }

    private void LogInformation(string action)
    {
        _logger.LogInformation($"@{FromUsername} {action} {_song.GetCaption()}.");
    }
}