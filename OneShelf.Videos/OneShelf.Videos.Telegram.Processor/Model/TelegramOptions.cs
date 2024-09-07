namespace OneShelf.Videos.Telegram.Processor.Model;

public record TelegramOptions : OneShelf.Telegram.Options.TelegramOptions
{
    public required string Token { get; init; }

    public required string WebHooksSecretToken { get; init; }
}