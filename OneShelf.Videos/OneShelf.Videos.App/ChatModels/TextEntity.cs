namespace OneShelf.Videos.App.ChatModels;

public class TextEntity
{
    public required string Type { get; set; }
    public required string Text { get; set; }
    public long? UserId { get; set; }
    public string? Href { get; set; }
    public string? DocumentId { get; set; }
    public bool? Collapsed { get; set; }
}