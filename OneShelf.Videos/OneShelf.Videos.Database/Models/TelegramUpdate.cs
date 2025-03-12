namespace OneShelf.Videos.Database.Models;

public class TelegramUpdate
{
    public int Id { get; set; }

    public required int TelegramUpdateId { get; set; }

    public required DateTime CreatedOn { get; set; }

    public required string Json { get; set; }
}