using Microsoft.EntityFrameworkCore;

namespace OneShelf.OneDog.Database.Model;

[Owned]
public class ImagesLimit
{
    public required TimeSpan Window { get; init; }
    
    public required int Limit { get; init; }
}