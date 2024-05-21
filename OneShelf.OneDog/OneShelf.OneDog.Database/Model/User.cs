using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OneShelf.OneDog.Database.Model;

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long Id { get; init; }

    public required string Title { get; set; }

    public ICollection<Interaction> Interactions { get; init; } = null!;

    public ICollection<Domain> AdministratedDomains { get; init; } = null!;

    public required DateTime CreatedOn { get; init; }
}