namespace OneShelf.Admin.Web.Models;

public record PngModel
{
    public required Uri Url1024 { get; init; }
    public required string Title { get; init; }
}