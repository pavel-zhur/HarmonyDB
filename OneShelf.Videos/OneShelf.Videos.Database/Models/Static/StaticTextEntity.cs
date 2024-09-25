namespace OneShelf.Videos.Database.Models.Static;

[Obsolete("Actually used to be referenced by the StaticMessage (TextEntities was a collection of it). This type may be useful in the future.")]
public class StaticTextEntity
{
    public required string Type { get; set; }
    public required string Text { get; set; }
    public long? UserId { get; set; }
    public string? Href { get; set; }
    public string? DocumentId { get; set; }
    public bool? Collapsed { get; set; }
}