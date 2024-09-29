using System.ComponentModel.DataAnnotations.Schema;

namespace OneShelf.Videos.Database.Models.Live;

public class LiveChat
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long Id { get; set; }

    public string Title { get; set; }

    public ICollection<LiveTopic> LiveTopics { get; set; }

    public ICollection<Media> Mediae { get; set; }

    public ICollection<Topic> Topics { get; set; }
}