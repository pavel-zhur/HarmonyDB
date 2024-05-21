using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace OneShelf.Illustrations.Database.Models;

public class IllustrationsPrompts : IValidatableObject
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required Guid Id { get; set; }
    
    public required string Url { get; set; }

    public required string Lyrics { get; set; }

    public required List<List<string>> Prompts { get; init; }

    [Column(TypeName = "nvarchar(100)")]
    public required string Version { get; set; }
    
    public required DateTime CreatedOn { get; set; }

    public required object Trace { get; init; }

    public int? SpecialSystemMessage { get; init; }

    public string? CustomSystemMessage { get; init; }

    public string? AlterationKey { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AlterationKey != null && !SpecialSystemMessage.HasValue)
            yield return new(
                $"The {nameof(AlterationKey)} is only supported for the {nameof(SpecialSystemMessage)}-d prompts.");

        if (Prompts == null! || Prompts.Contains(null!) || Prompts.Any(x => x.Contains(null!)))
            yield return new("Null prompts.");

        if ((SpecialSystemMessage.HasValue) == (CustomSystemMessage != null))
            yield return new($"Exactly one of {nameof(SpecialSystemMessage)}, {nameof(CustomSystemMessage)} should be set.");
    }
}