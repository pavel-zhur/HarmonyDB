﻿using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OneShelf.OneDragon.Database.Model;

public class Chat
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long Id { get; init; }

    public required string Type { get; init; }

    public string? Title { get; set; }

    public ICollection<Interaction> Interactions { get; init; } = null!;
}