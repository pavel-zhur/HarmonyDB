using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace HarmonyDB.Common.Representations.OneShelf;

/// <summary>
/// A key: its tonic as written (a letter, then its sharps or flats: "F#", "Bb", "C") and whether it is minor.
/// </summary>
public record SheetKey
{
    private static readonly IReadOnlyDictionary<char, int> LetterFifths = new Dictionary<char, int>
    {
        { 'F', -1 }, { 'C', 0 }, { 'G', 1 }, { 'D', 2 }, { 'A', 3 }, { 'E', 4 }, { 'B', 5 },
    };

    [JsonConstructor]
    public SheetKey(string tonic, bool isMinor)
    {
        if (!Regex.IsMatch(tonic, "^[A-G](#+|b+)?$"))
            throw new ArgumentOutOfRangeException(nameof(tonic), tonic, "A letter from A to G, then its sharps or its flats, is expected.");

        Tonic = tonic;
        IsMinor = isMinor;
    }

    public string Tonic { get; }

    public bool IsMinor { get; }

    /// <summary>
    /// The key's name: "F#m", "Bb".
    /// </summary>
    [JsonIgnore]
    public string Name => IsMinor ? $"{Tonic}m" : Tonic;

    /// <summary>
    /// The sharps (positive) or flats (negative) of its key signature: 3 for A and F#m, -2 for Bb and Gm.
    /// </summary>
    [JsonIgnore]
    public int Signature => LetterFifths[Tonic[0]] + 7 * Accidentals - (IsMinor ? 3 : 0);

    private int Accidentals => Tonic.Length == 1 ? 0 : Tonic[1] == '#' ? Tonic.Length - 1 : 1 - Tonic.Length;

    /// <summary>
    /// The note of the tonic, as <see cref="Note.Value"/>.
    /// </summary>
    [JsonIgnore]
    public byte Value => Note.CharactersToNotes[Tonic[0]].Add(Accidentals).Value;

    /// <summary>
    /// Reads a key's name: "F#m", "Bb", "Am".
    /// </summary>
    public static SheetKey Parse(string name) => name.EndsWith('m') ? new(name[..^1], true) : new(name, false);

    /// <summary>
    /// The key as the sheet shows it: its tonic named as the chords' roots are under these settings (transposed with them,
    /// with the sharps or flats asked for, as written when they are shown as written), so that the key and its chords agree.
    /// </summary>
    public SheetKey Shown(RepresentationSettings representationSettings)
    {
        var tonic = new NodeNote(new Note(Value, Accidentals > 0 ? NoteAlteration.Sharp : Accidentals < 0 ? NoteAlteration.Flat : null))
            {
                Accidentals = Accidentals,
            }
            .Representation(representationSettings with { RelativeTo = null, TestOnlyX = false });

        return new(tonic, IsMinor);
    }

    /// <summary>
    /// Whether the key has a key signature to draw: at most seven sharps or flats (G# has eight and has none).
    /// </summary>
    [JsonIgnore]
    public bool HasSignature => Math.Abs(Signature) <= 7;
}
