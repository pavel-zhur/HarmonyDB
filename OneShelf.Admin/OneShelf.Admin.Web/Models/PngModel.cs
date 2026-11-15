namespace OneShelf.Admin.Web.Models;

public record PngModel
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
}