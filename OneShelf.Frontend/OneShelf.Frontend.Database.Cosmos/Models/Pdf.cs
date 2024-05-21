using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace OneShelf.Frontend.Database.Cosmos.Models;

public class Pdf : IValidatableObject
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; set; }

    public required string ExternalId { get; set; }

    public required string ExportConfiguration { get; set; }

    [Column(TypeName = "nvarchar(100)")]
    public required string Version { get; set; }

    public required int PageCount { get; set; }

    public DateTime CreatedOn { get; set; }

    public required byte[] Data { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id != GetId((ExternalId, ExportConfiguration, Version)))
            yield return new("The ID is not correct.");
    }

    public static string GetId((string externalId, string exportConfiguration, string version) compound) =>
        JsonConvert.SerializeObject(compound);

    public static (string externalId, string exportConfiguration, string version) GetIds(string compound) =>
        JsonConvert.DeserializeObject<(string externalId, string exportConfiguration, string version)>(compound);
}