using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace OneShelf.Videos.App.Database.Models.Json;

public class Chat
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required List<Message> Messages { get; set; }

    [JsonIgnore]
    public int ChatFolderId { get; set; }

    [JsonIgnore]
    public ChatFolder ChatFolder { get; set; } = null!;
}