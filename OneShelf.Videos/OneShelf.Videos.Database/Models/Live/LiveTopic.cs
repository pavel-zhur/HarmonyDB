using Microsoft.EntityFrameworkCore;

namespace OneShelf.Videos.Database.Models.Live;

[PrimaryKey(nameof(Id), nameof(LiveChatId))]
public class LiveTopic
{
    public int Id { get; set; }

    public long LiveChatId { get; set; }

    public LiveChat LiveChat { get; set; } = null!;

    public string Title { get; set; }

    public ICollection<LiveMedia> LiveMediae { get; set; }
}