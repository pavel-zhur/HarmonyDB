﻿namespace HarmonyDB.Common.Representations.OneShelf;

public record Note
{
    public const int Modulus = 12;

    public byte Value { get; init; }
    public NoteAlteration? Alteration { get; init; }

    public Note()
    {
    }

    public Note(byte value, NoteAlteration? alteration = null)
    {
        Value = value;
        Alteration = alteration;
    }

    public static Note A { get; } = new(0);
    public static Note B { get; } = new(2);
    public static Note C { get; } = new(3);
    public static Note D { get; } = new(5);
    public static Note E { get; } = new(7);
    public static Note F { get; } = new(8);
    public static Note G { get; } = new(10);

    public static readonly IReadOnlyList<byte> Major = new List<byte>
    {
        A.Value,
        B.Value,
        (byte)(C.Value + 1),
        D.Value,
        E.Value,
        (byte)(F.Value + 1),
        (byte)(G.Value + 1),
    };

    public static readonly IReadOnlyList<byte> Minor = new List<byte>
    {
        A.Value,
        B.Value,
        C.Value,
        D.Value,
        E.Value,
        F.Value,
        G.Value,
    };

    public static IReadOnlyDictionary<char, Note> CharactersToNotes { get; } = new Dictionary<char, Note>
    {
        { 'A', A },
        { 'B', B },
        { 'C', C },
        { 'С', C }, // russian
        { 'D', D },
        { 'E', E },
        { 'F', F },
        { 'G', G },
        { 'H', B },
    };

    private static IReadOnlyDictionary<byte, char> NotesToCharacters { get; } = CharactersToNotes
        .Where(x => x.Key is not ('H' or 'С')) // russian 'С'
        .ToDictionary(x => x.Value.Value, x => x.Key);

    public static Note Max { get; } = new(11);
    public static Note Min { get; } = A;

    public static readonly IReadOnlyList<(Note major, Note minor, bool isSharps)> CircleOfFifths;

    static Note()
    {
        var circleOfFifths = new List<(Note, Note, bool isSharps)>();

        var current = new Note(3).Add(-7);
        for (var i = 0; i < Modulus; i++)
        {
            var isSharps = i <= 6;

            current = current.Add(7);

            Note ApplyAlteration(Note x) =>
                x with
                {
                    Alteration = Note.Minor.Contains(x.Value) ? null :
                    isSharps ? NoteAlteration.Sharp : NoteAlteration.Flat
                };

            current = ApplyAlteration(current);

            circleOfFifths.Add((current, ApplyAlteration(current.Add(-3)), isSharps));
        }
        
        CircleOfFifths = circleOfFifths;
    }

    public Note Sharp() => new(
        Value == Max.Value ? Min.Value : (byte)(Value + 1),
        NoteAlteration.Sharp);

    public Note Flat() => new(
        Value == Min.Value ? Max.Value : (byte)((Value + Modulus - 1) % Modulus),
        NoteAlteration.Flat);

    public Note Add(int delta)
    {
        var newValue = Value + delta;
        newValue %= Modulus;
        if (newValue < Min.Value)
        {
            newValue += Modulus;
        }

        return new((byte)newValue);
    }

    public string Representation(RepresentationSettings representationSettings)
    {
        if (representationSettings.TestOnlyX) return "X";

        if (representationSettings.RelativeTo.HasValue)
        {
            var (note, major) = representationSettings.RelativeTo.Value;
            var scale = major ? Major : Minor;
            scale = scale.Select(n => new Note(n).Add(note).Add(-representationSettings.Transpose).Value).ToList();
            var found = scale.Select((x, i) => (x, i: i + 1)).SingleOrDefault(x => x.x == Value).i;
            if (found > 0) return found.ToString();
        }

        var value = Value;
        var preferredAlteration = Alteration;
        if (representationSettings.Transpose != 0)
        {
            value = (byte)((value + Modulus + representationSettings.Transpose) % Modulus);
            preferredAlteration = null;
        }

        if (NotesToCharacters.TryGetValue(value, out var result)) return result.ToString();

        preferredAlteration = representationSettings.Alteration ?? preferredAlteration;

        preferredAlteration ??= NoteAlteration.Sharp;

        switch (preferredAlteration)
        {
            case NoteAlteration.Flat:
                return $"{NotesToCharacters[(byte)(value == Max.Value ? Min.Value : value + 1)]}{SignsConstants.SignFlat}";

            case NoteAlteration.Sharp:
                return $"{NotesToCharacters[(byte)(value == Min.Value ? Max.Value : value - 1)]}{SignsConstants.SignSharp}";

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public byte Tone(RepresentationSettings representationSettings)
    {
        var value = Value;
        if (representationSettings.Transpose != 0)
        {
            value = (byte)((value + Modulus + representationSettings.Transpose) % Modulus);
        }

        return value;
    }

    public int Length() => Alteration.HasValue ? 2 : 1;

    public int EstimatedLength(RepresentationSettings representationSettings) => Representation(representationSettings).Length;

    public static byte Normalize(int delta) => (byte)(((delta % Modulus) + Modulus + Modulus) % Modulus);
}