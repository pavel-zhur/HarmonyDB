using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace OneShelf.Videos.Database.Models.Live;

[PrimaryKey(nameof(Id), nameof(LiveTopicLiveChatId))]
public class LiveMedia
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public int LiveTopicId { get; set; }

    public long LiveTopicLiveChatId { get; set; }

    public LiveTopic LiveTopic { get; set; }

    public LiveMediaType Type { get; set; }

    public Media? Media { get; set; }

    public MediaType? SelectedType { get; set; }

    public bool LastInventoryExists { get; set; }

    public DateTime MessageDate { get; set; }
    public bool IsForwarded { get; set; }
    public DateTime MediaDate { get; set; }
    public long MediaId { get; set; }
    public string? FileName { get; set; }
    public string MediaFlags { get; set; }
    public string? MimeType { get; set; }
    public long Size { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double? Duration { get; set; }
    public string? VideoFlags { get; set; }
    public string MediaType { get; set; }
    public string? DocumentAttributes { get; set; }
    public string? DocumentAttributeTypes { get; set; }
    public string? Flags { get; set; }
}