using OneShelf.Common;
using OneShelf.Videos.Database.Models;
using Telegram.BotAPI.AvailableTypes;

namespace OneShelf.Videos.Telegram.Processor.Services;

public class HandlersConstructor
{
    public (string text, InlineKeyboardMarkup markup) GetHandlerMessage(List<TelegramMedia> mediae, Dictionary<Tag, bool> tags)
        => ("#handler " + string.Join(
                " + ",
                mediae
                    .WithPrevious()
                    .ToChunksByShouldStartNew(p => p.current.MediaGroupId != p.previous?.MediaGroupId)
                    .Select(g => g.Count)),
            new(tags.Chunk(2).Select(x => x.Select(x => new InlineKeyboardButton($"{(x.Value ? "\u2713 " : "")}{x.Key.Title}") { CallbackData = x.Key.Id.ToString(), }))));
}