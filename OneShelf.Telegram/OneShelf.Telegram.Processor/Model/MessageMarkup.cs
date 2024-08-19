using OneShelf.Telegram.Model;
using Telegram.BotAPI.AvailableTypes;

namespace OneShelf.Telegram.Processor.Model;

public class MessageMarkup
{
    public required Markdown BodyOrCaption { get; init; }

    public string? FileId { get; init; }

    public InlineKeyboardMarkup? InlineKeyboardMarkup { get; init; }

    public bool Pin { get; init; }
}