using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace OneShelf.Videos.Database.Models.Static;

[Index(nameof(StaticChatFolderId), IsUnique = true)]
public class StaticChat
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required List<StaticMessage> Messages { get; set; }

    [JsonIgnore]
    public int StaticChatFolderId { get; set; }

    [JsonIgnore]
    public StaticChatFolder StaticChatFolder { get; set; } = null!;

    [JsonIgnore]
    public ICollection<Media> Mediae { get; set; }
}