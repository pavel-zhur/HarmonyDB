namespace OneShelf.OneDog.Processor.Model;

public record TelegramOptions
{
    public long AdminId { get; set; }
    
    public bool IsAdmin(long userId) => AdminId == userId;
}