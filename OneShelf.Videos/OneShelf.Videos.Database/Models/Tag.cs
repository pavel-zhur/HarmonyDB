namespace OneShelf.Videos.Database.Models;

public class Tag
{
    public int Id { get; set; }

    public string Title { get; set; }

    public ICollection<TelegramMedia> TelegramMediae { get; set; } = null!;
}