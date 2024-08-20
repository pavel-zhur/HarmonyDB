namespace OneShelf.Videos.App.ChatModels;

public class Chat
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required long Id { get; set; }
    public required List<Message> Messages { get; set; }
}