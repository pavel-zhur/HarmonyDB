namespace OneShelf.Illustrations.Database.Models;

public class IllustrationHeader
{
    public required Guid Id { get; set; }
    public required int AttemptIndex { get; set; }
    public required int PhotoIndex { get; set; }
    public required DateTime CreatedOn { get; set; }
    public required Guid PromptsId { get; set; }
    public required string Url { get; set; }
}