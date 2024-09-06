using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OneShelf.OneDragon.Database.Model;

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long Id { get; init; }

    public required string Title { get; set; }

    public ICollection<Interaction> Interactions { get; init; } = null!;

    public required DateTime CreatedOn { get; init; }
    
    public string? FirstName { get; set; }
    
    public string? LastName { get; set; }
    
    public string? UserName { get; set; }
    
    public string? LanguageCode { get; set; }

    public required bool UseLimits { get; set; }
}