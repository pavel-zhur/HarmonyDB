namespace OneShelf.Telegram.Processor.Model;

public record TelegramOptions
{
    public string Token { get; set; } = null!;

    public string FilesChatId { get; set; } = null!;

    public string PublicChatId { get; set; } = null!;

    public int PublicTopicId { get; set; }

    public int AnnouncementsTopicId { get; set; }

    public long AdminId { get; set; }

    public string WebHooksSecretToken { get; set; } = null!;

    /// <summary>
    /// Used for testing.
    /// </summary>
    public bool MeIsNotMe { get; set; }

    public long BotId => long.Parse(Token.Substring(0, Token.IndexOf(':')));

    public string BotUsername { get; set; } = null!;

    public int PublicChatterTopicId { get; set; }
    
    public int OwnChatterTopicId { get; set; }

    public bool IsAdmin(long userId) => AdminId == userId && !MeIsNotMe;

    public string NeverPromoteResponseStickerFileId { get; set; }

    public required int TenantId { get; set; }

    public required string[] ReactToUrls { get; set; }
}