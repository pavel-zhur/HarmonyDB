namespace OneShelf.OneDragon.Processor.Model;

public record TelegramOptions : Telegram.Options.TelegramOptions
{
    public required string Token { get; init; }

    public required string WebHooksSecretToken { get; init; }

    public long BotId => long.Parse(Token.Substring(0, Token.IndexOf(':')));
}