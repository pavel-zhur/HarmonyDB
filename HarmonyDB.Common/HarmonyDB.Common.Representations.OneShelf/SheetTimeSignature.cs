using System.Text.Json.Serialization;

namespace HarmonyDB.Common.Representations.OneShelf;

/// <summary>
/// A time signature: so many beats in a bar (<see cref="Beats"/>), each the note <see cref="Unit"/> (4 a quarter, 8 an eighth…): 3/4, 6/8.
/// </summary>
public record SheetTimeSignature
{
    [JsonConstructor]
    public SheetTimeSignature(int beats, int unit)
    {
        if (beats <= 0)
            throw new ArgumentOutOfRangeException(nameof(beats), beats, "A positive number of beats is expected.");

        if (unit is not (1 or 2 or 4 or 8 or 16 or 32))
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "A note value is expected: 1, 2, 4, 8, 16 or 32.");

        Beats = beats;
        Unit = unit;
    }

    public int Beats { get; }

    public int Unit { get; }

    /// <summary>
    /// Reads "3/4".
    /// </summary>
    public static SheetTimeSignature Parse(string text)
    {
        var parts = text.Split('/');
        return parts.Length == 2 && int.TryParse(parts[0], out var beats) && int.TryParse(parts[1], out var unit)
            ? new(beats, unit)
            : throw new ArgumentOutOfRangeException(nameof(text), text, "A time signature like 3/4 is expected.");
    }

    public override string ToString() => $"{Beats}/{Unit}";
}
