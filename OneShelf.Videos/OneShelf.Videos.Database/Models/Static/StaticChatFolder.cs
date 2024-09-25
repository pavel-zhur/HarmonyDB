using Microsoft.EntityFrameworkCore;

namespace OneShelf.Videos.Database.Models.Static;

[Index(nameof(Name), IsUnique = true)]
public class StaticChatFolder
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Root { get; set; }

    public string ResultJsonFullPath => Path.Combine(Root, "result.json");

    public StaticChat? StaticChat { get; set; }
}