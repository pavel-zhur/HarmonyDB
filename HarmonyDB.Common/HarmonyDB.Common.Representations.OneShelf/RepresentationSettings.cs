namespace HarmonyDB.Common.Representations.OneShelf;

public record RepresentationSettings
{
    public RepresentationSettings(int transpose = 0, NoteAlteration? alteration = null, bool testOnlyX = false, bool isVariableWidth = false, SimplificationMode simplification = SimplificationMode.None, IReadOnlyDictionary<int, string>? customAttributes = null)
    {
        Transpose = transpose > 7 ? transpose - Note.Modulus : transpose < -7 ? transpose + Note.Modulus : transpose;
        Alteration = alteration;
        TestOnlyX = testOnlyX;
        IsVariableWidth = isVariableWidth;
        Simplification = simplification;
        CustomAttributes = customAttributes ?? new Dictionary<int, string>();
    }

    public int Transpose { get; init; }
    public NoteAlteration? Alteration { get; init; }
    public bool TestOnlyX { get; init; }
    public (int note, bool major)? RelativeTo { get; init; }
    public bool IsVariableWidth { get; init; }
    public SimplificationMode Simplification { get; init; }
    public IReadOnlyDictionary<int, string> CustomAttributes { get; init; }

    /// <summary>
    /// Show the chords' degrees (in the key where the chord stands), where the sheet has them.
    /// </summary>
    public bool ShowDegrees { get; init; } = true;

    /// <summary>
    /// Draw the chord names with their degrees lowered and their altered degrees raised (subscripts and superscripts):
    /// in Cm7b5 the 7 is a subscript and the b5 a superscript. Off, the chord names are plain text.
    /// </summary>
    public bool ChordSuperscripts { get; init; } = true;

    /// <summary>
    /// Not transposed, and with no sharps or flats asked for (<see cref="Alteration"/>), show the roots and the basses written with one sign
    /// as they are written: E#m, Cb, Am/Fb. Off, these are named by their pitch: Fm, B, Am/E.
    /// A note written with two signs (F##m, Bbb7) is shown as written either way.
    /// </summary>
    public bool ShowNotesAsWritten { get; init; }

    public RepresentationSettings Down() => new(Transpose - 1, Alteration, TestOnlyX, IsVariableWidth, Simplification, CustomAttributes)
    {
        ShowDegrees = ShowDegrees,
        ChordSuperscripts = ChordSuperscripts,
        ShowNotesAsWritten = ShowNotesAsWritten,
    };

    public RepresentationSettings Up() => new(Transpose + 1, Alteration, TestOnlyX, IsVariableWidth, Simplification, CustomAttributes)
    {
        ShowDegrees = ShowDegrees,
        ChordSuperscripts = ChordSuperscripts,
        ShowNotesAsWritten = ShowNotesAsWritten,
    };
}
