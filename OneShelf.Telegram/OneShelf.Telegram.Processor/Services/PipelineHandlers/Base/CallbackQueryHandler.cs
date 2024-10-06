using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers.Base;

public abstract class CallbackQueryHandler : PipelineHandler
{
    protected CallbackQueryHandler(IOptions<TelegramOptions> telegramOptions, SongsDatabase songsDatabase, IScopedAbstractions scopedAbstractions) 
        : base(scopedAbstractions)
    {
        TelegramOptions = telegramOptions.Value;
        SongsDatabase = songsDatabase;
    }

    protected TelegramOptions TelegramOptions { get; }

    protected SongsDatabase SongsDatabase { get; }

    protected abstract IReadOnlyList<string> Catch { get; }

    protected abstract Task Handle(AnswerCallbackQueryArgs answerCallbackQueryArgs);
    protected virtual Task PostHandle() => Task.CompletedTask;

    protected byte CatchIndex { get; private set; }
    protected long UserId { get; private set; }
    protected int SongId { get; private set; }
    protected string? FromUsername { get; private set; }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (update.CallbackQuery?.Data == null) return false;

        var chatFound = TelegramOptions.PublicLibraryChatId.Substring(1) == update.CallbackQuery?.Message?.Chat.Username;

        if (!chatFound) return false;

        var catchIndex = Catch
            .Select((x, i) => (x, (int?)i))
            .SingleOrDefault(x => x.x == update.CallbackQuery!.Data)
            .Item2 ?? -1;

        if (catchIndex == -1) return false;

        CatchIndex = (byte)catchIndex;

        UserId = update.CallbackQuery.From.Id;

        SongId = await SongsDatabase.Messages
            .Where(x => x.TenantId == TelegramOptions.TenantId)
            .Where(x => x.Type == MessageType.Song)
            .Where(x => x.MessageId == update.CallbackQuery.Message.MessageId)
            .Select(x => x.Song.Id)
            .SingleAsync();

        FromUsername = update.CallbackQuery.From.Username;

        var answerCallbackQueryArgs = new AnswerCallbackQueryArgs(update.CallbackQuery.Id);

        await Handle(answerCallbackQueryArgs);
        QueueApi(UserId.ToString(), async api =>
        {
            try
            {
                await api.AnswerCallbackQueryAsync(answerCallbackQueryArgs);
            }
            catch (BotRequestException e) when (e.Message.Contains("query is too old"))
            {
            }
        });
        await PostHandle();

        return true;
    }
}