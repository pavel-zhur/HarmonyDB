using Microsoft.Extensions.Options;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using Constants = OneShelf.Telegram.Processor.Helpers.Constants;
using TelegramOptions = OneShelf.Telegram.Processor.Model.TelegramOptions;

namespace OneShelf.Telegram.Processor.Services;

public class StringsCombiner
{
    private readonly TelegramOptions _telegramOptions;

    public StringsCombiner(IOptions<TelegramOptions> telegramOptions)
    {
        _telegramOptions = telegramOptions.Value;
    }

    public Markdown BuildUrl(Markdown title, int messageId, bool isAnnouncementsTopic = false) => Markdown.UnsafeFromMarkdownString($"[{title}]({GenerateMessageLink(messageId, isAnnouncementsTopic)})");

    public string GetManagementUri(int songIndex) =>
        new($"t.me/{_telegramOptions.BotUsername}?start={Constants.ImproveCommandName}-{songIndex}");

    public Markdown BuildManagementUrl(string title, int songIndex) => GetManagementUri(songIndex).BuildUrl(title);

    private string GenerateMessageLink(int messageId, bool isAnnouncementsTopic = false) =>
        $"t.me/{_telegramOptions.PublicChatId.Substring(1)}/{(isAnnouncementsTopic ? _telegramOptions.AnnouncementsTopicId : _telegramOptions.PublicTopicId)}/{messageId}";
}