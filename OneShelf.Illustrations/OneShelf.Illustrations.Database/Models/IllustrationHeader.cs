namespace OneShelf.Illustrations.Database.Models;

public class IllustrationHeader
{
    public required Guid Id { get; set; }
    public required int AttemptIndex { get; set; }
    public required int PhotoIndex { get; set; }
    public required DateTime CreatedOn { get; set; }
    public required Guid PromptsId { get; set; }
    public required string Url { get; set; }
    public Uri? PublicUrl1024 { get; set; }
    public Uri? PublicUrl512 { get; set; }
    public Uri? PublicUrl256 { get; set; }
    public Uri? PublicUrl128 { get; set; }
}