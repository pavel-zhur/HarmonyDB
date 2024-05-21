using System.Text.Json;
using OneShelf.Common;

namespace OneShelf.Frontend.SpecificModel;

public static class ConversionExtensions
{
    public static FrontendAttributesV1 ToFrontendAttributesV1(this IReadOnlyDictionary<string, object?> attributes)
        => new()
        {
            BadgeText = ((JsonElement?)attributes.GetValueOrDefault(nameof(FrontendAttributesV1.BadgeText)))?.GetString(),
            SourceColor = ((JsonElement)attributes[nameof(FrontendAttributesV1.SourceColor)]!).GetString()!.SelectSingle(Enum.Parse<SourceColor>),
        };

    public static void ToDictionary(this FrontendAttributesV1 attributes, Dictionary<string, object?> dictionary)
    {
        dictionary[nameof(attributes.SourceColor)] = attributes.SourceColor.ToString();
        
        if (attributes.BadgeText != null)
        {
            dictionary[nameof(attributes.BadgeText)] = attributes.BadgeText;
        }
    }
}