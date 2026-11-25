using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HarmonyDB.Shelf.Database.Model;

/// <summary>
/// Keeps an enum as text, its member's name in snake case (<c>OwnChords</c> → <c>own_chords</c>), so the database reads
/// plainly and a new member needs no migration.
/// </summary>
public sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(x => ToText(x), x => FromText(x))
    where TEnum : struct, Enum
{
    private static readonly FrozenDictionary<TEnum, string> Texts =
        Enum.GetValues<TEnum>().ToFrozenDictionary(x => x, x => JsonNamingPolicy.SnakeCaseLower.ConvertName(x.ToString()));

    private static readonly FrozenDictionary<string, TEnum> Values =
        Texts.ToFrozenDictionary(x => x.Value, x => x.Key);

    public static string ToText(TEnum value) => Texts[value];

    public static TEnum FromText(string text) => Values[text];
}
