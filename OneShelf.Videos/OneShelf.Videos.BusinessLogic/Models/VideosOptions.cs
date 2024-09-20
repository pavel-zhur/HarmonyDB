namespace OneShelf.Videos.BusinessLogic.Models;

public record VideosOptions
{
    public required string BasePath { get; init; }

    public required string TelegramAuthPath { get; init; }
    public required string TelegramApiId { get; init; }
    public required string TelegramApiHash { get; init; }
    public required string TelegramPhoneNumber { get; init; }
    public required IReadOnlyList<long> TelegramChatIds { get; init; }
}