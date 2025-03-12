using HarmonyDB.Theory.Chords.Constants;
using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Models;

public record NoteRepresentation(NaturalNoteRepresentation NaturalNoteRepresentation, int Sharps, int Flats)
{
    public Note Note => Note.Normalized((NaturalNoteRepresentation switch
    {
        >= 0 => (byte)NaturalNoteRepresentation,
        NaturalNoteRepresentation.GermanB => NoteConstants.GermanBNote.Value,
        NaturalNoteRepresentation.H => (byte)NoteConstants.HSynonym,
        >= NaturalNoteRepresentation.La and <= NaturalNoteRepresentation.Sol => NaturalNoteRepresentation - NaturalNoteRepresentation.La,
        _ => throw new ArgumentOutOfRangeException(nameof(NaturalNoteRepresentation), NaturalNoteRepresentation, "The representation is out of range."),
    }) + Sharps - Flats);
}