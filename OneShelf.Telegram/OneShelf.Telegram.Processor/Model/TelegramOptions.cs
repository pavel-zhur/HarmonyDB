namespace OneShelf.Telegram.Processor.Model;

public record TelegramOptions : Options.TelegramOptions
{
    public string Token { get; set; } = null!;

    public string FilesChatId { get; set; } = null!;

    public string PublicLibraryChatId { get; set; } = null!;
    
    public string PublicDogChatId { get; set; } = null!;
    
    public int PublicTopicId { get; set; }

    public int AnnouncementsTopicId { get; set; }

    public string WebHooksSecretToken { get; set; } = null!;

    /// <summary>
    /// Used for testing.
    /// </summary>
    public bool MeIsNotMe { get; set; }

    public long BotId => long.Parse(Token.Substring(0, Token.IndexOf(':')));

    public string BotUsername { get; set; } = null!;

    public int OwnChatterTopicId { get; set; }

    public string NeverPromoteResponseStickerFileId { get; set; }

    public required int TenantId { get; set; }

    public required string[] ReactToUrls { get; set; }
}