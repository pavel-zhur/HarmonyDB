namespace OneShelf.Telegram.Options;

public record TelegramOptions
{
    public long AdminId { get; set; }

    public bool IsAdmin(long userId) => AdminId == userId;
}