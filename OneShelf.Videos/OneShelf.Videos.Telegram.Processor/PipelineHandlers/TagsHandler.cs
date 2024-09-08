using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.Database;
using OneShelf.Videos.Telegram.Processor.Model;
using OneShelf.Videos.Telegram.Processor.Services;
using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;
using Telegram.BotAPI.UpdatingMessages;

namespace OneShelf.Videos.Telegram.Processor.PipelineHandlers;

public class TagsHandler(
    IScopedAbstractions scopedAbstractions,
    IOptions<TelegramOptions> telegramOptions,
    VideosDatabase videosDatabase,
    HandlersConstructor handlersConstructor) 
    : PipelineHandler(scopedAbstractions)
{
    protected override async Task<bool> HandleSync(Update update)
    {
        if (update.CallbackQuery?.Message == null
            || !telegramOptions.Value.IsAdmin(update.CallbackQuery.Message.Chat.Id))
        {
            return false;
        }

        QueueApi(null, api => Go(api, update));
        return true;
    }

    private async Task Go(TelegramBotClient api, Update update)
    {
        var mediae = await videosDatabase.TelegramMedia
            .Include(x => x.Tags)
            .Where(m =>
                m.HandlerMessageId == update.CallbackQuery!.Message!.MessageId
                && m.ChatId == update.CallbackQuery!.Message!.Chat.Id)
            .ToListAsync();

        var tag = await videosDatabase.Tags.SingleAsync(x => x.Id == int.Parse(update.CallbackQuery!.Data!));

        foreach (var media in mediae)
        {
            if (media.Tags.Contains(tag))
                media.Tags.Remove(tag);
            else
                media.Tags.Add(tag);
        }

        var usedTags = mediae.SelectMany(x => x.Tags).Select(x => x.Id).ToHashSet();

        await videosDatabase.SaveChangesAsync();

        var tags = (await videosDatabase.Tags.ToListAsync()).ToDictionary(x => x, x => usedTags.Contains(x.Id));

        var (text, markup) = handlersConstructor.GetHandlerMessage(mediae, tags);

        await api.EditMessageTextAsync(update.CallbackQuery!.Message!.Chat.Id, update.CallbackQuery.Message.MessageId, text, replyMarkup: markup);
    }
}