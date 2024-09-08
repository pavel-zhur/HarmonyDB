namespace OneShelf.Videos.Database.Models.Json;

[Obsolete("Actually used to be referenced by the Message (TextEntities was a collection of it). This type may be useful in the future.")]
public class TextEntity
{
    public required string Type { get; set; }
    public required string Text { get; set; }
    public long? UserId { get; set; }
    public string? Href { get; set; }
    public string? DocumentId { get; set; }
    public bool? Collapsed { get; set; }
}