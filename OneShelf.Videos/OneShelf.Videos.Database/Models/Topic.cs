using OneShelf.Videos.Database.Models.Live;
using OneShelf.Videos.Database.Models.Static;

namespace OneShelf.Videos.Database.Models;

public class Topic
{
    public int Id { get; set; }

    public long? StaticChatId { get; set; }
    public int? StaticTopicRootMessageIdOr0 { get; set; }

    public StaticChat? StaticChat { get; set; }
    public StaticTopic? StaticTopic { get; set; }

    public long? LiveChatId { get; set; }
    public int? LiveTopicId { get; set; }

    public LiveChat? LiveChat { get; set; }
    public LiveTopic? LiveTopic { get; set; }

    public ICollection<AlbumConstraint> AlbumConstraints { get; set; } = null!;
    public ICollection<Media> Mediae { get; set; } = null!;
}