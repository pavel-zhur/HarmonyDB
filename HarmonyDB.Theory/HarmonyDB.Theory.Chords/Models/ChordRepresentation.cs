namespace HarmonyDB.Theory.Chords.Models;

public record ChordRepresentation(
    NoteRepresentation RootRepresentation,
    NoteRepresentation? BassRepresentation)
{
    public Note Root => RootRepresentation.Note;
    public Note? Bass => BassRepresentation?.Note;
}