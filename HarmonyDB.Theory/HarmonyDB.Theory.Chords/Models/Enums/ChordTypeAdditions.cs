namespace HarmonyDB.Theory.Chords.Models.Enums;

[Flags]
public enum ChordTypeAdditions
{
    None = 0,

    Add2 = 1,
    Flat2 = 1 << 1,

    Add4 = 1 << 2,
    Sharp4 = 1 << 3,

    No5 = 1 << 4,
    Flat5 = 1 << 5,
    Sharp5 = 1 << 6,

    Add6 = 1 << 7,
    Flat6 = 1 << 8,

    No7 = 1 << 9,

    Add9 = 1 << 10,
    Flat9 = 1 << 11,
    Sharp9 = 1 << 12,
    No9 = 1 << 13,

    Add11 = 1 << 14,
    Flat11 = 1 << 15,
    Sharp11 = 1 << 16,
    No11 = 1 << 17,

    Add13 = 1 << 18,
    Flat13 = 1 << 19,
    Sharp13 = 1 << 20,
}