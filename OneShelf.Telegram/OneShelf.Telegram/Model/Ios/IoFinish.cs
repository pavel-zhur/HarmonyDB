using Telegram.BotAPI.AvailableTypes;

namespace OneShelf.Telegram.Model.Ios;

public class IoFinish
{
    public Markdown? ReplyMessageBody { get; set; }
    public ReplyMarkup? ReplyMessageMarkup { get; set; }

    public List<(Markdown markup, InlineKeyboardMarkup? inlineKeyboardMarkup)> AdditionalOutputs { get; } = new();
}