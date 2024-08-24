using Microsoft.EntityFrameworkCore;
using OneShelf.Videos.App.Database.Models.Json;

namespace OneShelf.Videos.App.Database.Models;

[Index(nameof(Name), IsUnique = true)]
public class ChatFolder
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Root { get; set; }

    public string ResultJsonFullPath => Path.Combine(Root, "result.json");

    public Chat? Chat { get; set; }
}