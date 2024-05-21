using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace OneShelf.Collectives.Database.Models;

public class Collective : IValidatableObject
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    public long CreatedByUserId { get; set; }

    public required List<Version> Versions { get; set; }

    public CollectiveVisibility LatestVisibility { get; set; }
    
    public Uri? DerivedFromUri { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var version in Versions)
        {
            foreach (var result in version.Validate(validationContext))
            {
                yield return result;
            }
        }
    }
}