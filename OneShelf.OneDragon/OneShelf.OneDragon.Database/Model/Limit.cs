using System.ComponentModel.DataAnnotations.Schema;

namespace OneShelf.OneDragon.Database.Model;

public class Limit
{
    public int Id { get; set; }

    public int? Images { get; set; }

    public int? Texts { get; set; }

    public int? Videos { get; set; }

    public int? Songs { get; set; }

    [Column(TypeName = "bigint")]
    public required TimeSpan Window { get; set; }

    public bool IsEnabled { get; set; }
}