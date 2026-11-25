using System.Text.Json.Serialization;

namespace HarmonyDB.Common.Representations.OneShelf;

/// <summary>
/// A tempo: so many beats a minute, a beat being the note <see cref="Unit"/> (4 a quarter, 8 an eighth…), dotted or not.
/// </summary>
public record SheetTempo
{
    [JsonConstructor]
    public SheetTempo(double beatsPerMinute, int unit = 4, bool dotted = false)
    {
        if (beatsPerMinute <= 0)
            throw new ArgumentOutOfRangeException(nameof(beatsPerMinute), beatsPerMinute, "A positive tempo is expected.");

        if (unit is not (1 or 2 or 4 or 8 or 16))
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "A whole, a half, a quarter, an eighth or a sixteenth note is expected: 1, 2, 4, 8 or 16.");

        BeatsPerMinute = beatsPerMinute;
        Unit = unit;
        Dotted = dotted;
    }

    public double BeatsPerMinute { get; }

    public int Unit { get; }

    public bool Dotted { get; }
}
