using OneShelf.Common;
using Telegram.BotAPI.AvailableTypes;

namespace OneShelf.Telegram.Helpers;

public static class UserTitleHelper
{
    public static string GetTitle(this User user)
        => string.Join(" ", user.FirstName, user.LastName).SelectSingle(x => string.IsNullOrWhiteSpace(x) ? null : x.Trim())
           ?? user.Username
           ?? user.Id.ToString();

    public static string GetTitle(this Chat chat)
        => chat.Title 
           ?? string.Join(" ", chat.FirstName, chat.LastName).SelectSingle(x => string.IsNullOrWhiteSpace(x) ? null : x.Trim())
           ?? chat.Username
           ?? chat.Id.ToString();

    public static string? GetTitle(this MessageOrigin? messageOrigin)
        => string.Join(" ",
            messageOrigin switch
            {
                null => null,
                MessageOriginChannel messageOriginChannel => messageOriginChannel.Chat.GetTitle(),
                MessageOriginChat messageOriginChat => messageOriginChat.SenderChat.GetTitle(),
                MessageOriginHiddenUser messageOriginHiddenUser => messageOriginHiddenUser.SenderUserName,
                MessageOriginUser messageOriginUser => messageOriginUser.SenderUser.GetTitle(),
                _ => throw new ArgumentOutOfRangeException(nameof(messageOrigin))
            },
            messageOrigin switch
            {
                null => null,
                MessageOriginChannel messageOriginChannel => messageOriginChannel.AuthorSignature,
                MessageOriginChat messageOriginChat => messageOriginChat.AuthorSignature,
                MessageOriginHiddenUser => null,
                MessageOriginUser => null,
                _ => throw new ArgumentOutOfRangeException(nameof(messageOrigin))
            }).SelectSingle(x => string.IsNullOrWhiteSpace(x) ? null : x.Trim());
}