namespace HarmonyDB.Theory.Chords.Models;

public record Chord(NoteRepresentation RootRepresentation, NoteRepresentation? BassRepresentation, string ChordTypeRepresentation)
{
    public Note Root = RootRepresentation.Note;
    public Note? Bass = BassRepresentation?.Note;
}