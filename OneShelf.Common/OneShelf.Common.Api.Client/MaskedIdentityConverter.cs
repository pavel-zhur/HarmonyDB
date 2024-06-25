using System.Text.Json;
using System.Text.Json.Serialization;
using OneShelf.Authorization.Api.Model;

namespace OneShelf.Common.Api.Client;

internal class MaskedIdentityConverter : JsonConverter<Identity>
{
    public override Identity? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }

    public override void Write(Utf8JsonWriter writer, Identity value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, new Identity
        {
            Hash = "(service call hash)",
        });
    }
}