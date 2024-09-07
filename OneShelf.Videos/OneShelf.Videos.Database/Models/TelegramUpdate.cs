using System.ComponentModel.DataAnnotations.Schema;

namespace OneShelf.Videos.Database.Models;

public class TelegramUpdate
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required int Id { get; set; }

    public required DateTime CreatedOn { get; set; }

    public required string Json { get; set; }
}