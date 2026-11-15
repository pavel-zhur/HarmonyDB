using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OneShelf.Common.Database;

/// <summary>
/// The JSON of document-shaped values: Newtonsoft.Json with camelCase names and enums as numbers, as the Cosmos DB client
/// wrote and read these documents before they moved to PostgreSQL.
/// </summary>
public static class DocumentJson
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };

    /// <summary>
    /// Keeps the property in a jsonb column, as <see cref="Settings"/> write it. PostgreSQL only.
    /// </summary>
    public static PropertyBuilder<T> HasJsonbConversion<T>(this PropertyBuilder<T> property)
        => property
            .HasColumnType("jsonb")
            .HasConversion(
                x => Serialize(x),
                x => Deserialize<T>(x),
                new ValueComparer<T>(
                    (x, y) => Serialize(x) == Serialize(y),
                    x => Serialize(x).GetHashCode(),
                    x => Deserialize<T>(Serialize(x))));

    public static string Serialize<T>(T value) => JsonConvert.SerializeObject(value, Settings);

    public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings)!;
}
