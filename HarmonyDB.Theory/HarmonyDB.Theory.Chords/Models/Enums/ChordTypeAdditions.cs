namespace HarmonyDB.Theory.Chords.Models.Enums;

[Flags]
public enum ChordTypeAdditions
{
    None = 0,

    Add2 = 1,

    Add4 = 1 << 1,
    Sharp4 = 1 << 2,

    No5 = 1 << 3,
    Flat5 = 1 << 4,
    Sharp5 = 1 << 5,

    Add6 = 1 << 6,
    Flat6 = 1 << 7,

    No7 = 1 << 8,

    Add9 = 1 << 9,
    Flat9 = 1 << 10,
    Sharp9 = 1 << 11,
    No9 = 1 << 12,

    Add11 = 1 << 13,
    Flat11 = 1 << 14,
    Sharp11 = 1 << 15,
    No11 = 1 << 16,

    Add13 = 1 << 17,
    Flat13 = 1 << 18,
    Sharp13 = 1 << 19,

    HalfDiminished7 = 1 << 20,
}