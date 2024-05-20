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

    public RepresentationSettings Down() => new(Transpose - 1, Alteration, TestOnlyX, IsVariableWidth, Simplification, CustomAttributes);
    public RepresentationSettings Up() => new(Transpose + 1, Alteration, TestOnlyX, IsVariableWidth, Simplification, CustomAttributes);
}