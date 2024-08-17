using OneShelf.Telegram.Model;
using Telegram.BotAPI.AvailableTypes;

namespace OneShelf.OneDog.Processor.Model.Ios;

public class IoFinish
{
    public Markdown? ReplyMessageBody { get; set; }
    public ReplyMarkup? ReplyMessageMarkup { get; set; }

    public List<(Markdown markup, InlineKeyboardMarkup? inlineKeyboardMarkup)> AdditionalOutputs { get; } = new();
}