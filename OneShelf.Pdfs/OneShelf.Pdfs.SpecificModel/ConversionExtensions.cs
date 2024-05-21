using System.Text.Json;

namespace OneShelf.Pdfs.SpecificModel;

public static class ConversionExtensions
{
    public static PdfsAttributes ToPdfsAttributes(this IReadOnlyDictionary<string, object?> attributes)
        => new()
        {
            ShortSourceName = ((JsonElement?)attributes.GetValueOrDefault(nameof(PdfsAttributes.ShortSourceName)))?.GetString() ?? throw new($"The {nameof(PdfsAttributes.ShortSourceName)} key is required for the pdfs conversion."),
        };

    public static void ToDictionary(this PdfsAttributes attributes, Dictionary<string, object?> dictionary)
    {
        dictionary.Add(nameof(attributes.ShortSourceName), attributes.ShortSourceName);
    }
}