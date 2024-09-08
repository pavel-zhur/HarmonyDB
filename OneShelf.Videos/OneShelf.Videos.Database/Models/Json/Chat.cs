using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace OneShelf.Videos.Database.Models.Json;

[Index(nameof(ChatFolderId), IsUnique = true)]
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