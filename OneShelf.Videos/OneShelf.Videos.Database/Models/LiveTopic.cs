using Microsoft.EntityFrameworkCore;

namespace OneShelf.Videos.Database.Models;

[PrimaryKey(nameof(Id), nameof(ChatId))]
public class LiveTopic
{
    public int Id { get; set; }

    public long ChatId { get; set; }

    public LiveChat Chat { get; set; } = null!;

    public string Title { get; set; }

    public ICollection<LiveMedia> Mediae { get; set; }
}