using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace OneShelf.OneDog.Database.Model;

[Owned]
public class MediaLimit
{
    [Column(TypeName = "bigint")]
    public required TimeSpan Window { get; init; }
    
    public required int Limit { get; init; }
}