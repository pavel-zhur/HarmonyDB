using System.ComponentModel.DataAnnotations.Schema;

namespace OneShelf.OneDragon.Database.Model;

public class Update
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required int Id { get; set; }

    public required DateTime CreatedOn { get; set; }

    public required string Json { get; set; }

    public ICollection<Interaction> Interactions { get; set; } = null!;
}