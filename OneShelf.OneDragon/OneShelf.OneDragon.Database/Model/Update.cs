namespace OneShelf.OneDragon.Database.Model;

public class Update
{
    public int Id { get; set; }

    public required DateTime CreatedOn { get; set; }

    public required string Json { get; set; }

    public ICollection<Interaction> Interactions { get; set; } = null!;
}