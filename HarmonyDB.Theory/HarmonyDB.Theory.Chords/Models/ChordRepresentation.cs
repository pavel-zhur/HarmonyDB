namespace HarmonyDB.Theory.Chords.Models;

public record ChordRepresentation(
    NoteRepresentation RootRepresentation,
    NoteRepresentation? BassRepresentation,
    ChordType ChordType)
{
    public Note Root => RootRepresentation.Note;
    public Note? Bass => BassRepresentation?.Note;
}