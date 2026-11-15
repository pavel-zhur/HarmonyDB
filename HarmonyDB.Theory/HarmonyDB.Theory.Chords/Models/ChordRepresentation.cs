using HarmonyDB.Theory.Chords.Constants;

namespace HarmonyDB.Theory.Chords.Models;

public record ChordRepresentation(
    NoteRepresentation RootRepresentation,
    NoteRepresentation? BassRepresentation,
    ChordType ChordType)
{
    public Note Root => RootRepresentation.Note;
    public Note? Bass => BassRepresentation?.Note;

    /// <summary>
    /// The pitch classes of the chord (the root first, then ascending from it), without the bass.
    /// </summary>
    public IReadOnlyList<Note> GetNotes()
        => ChordType.GetIntervals().Select(x => Note.Normalized(Root.Value + x)).ToList();

    /// <summary>
    /// The canonical name: the canonical root, the canonical chord type, and the canonical bass after a slash.
    /// Two notations of the same chord give the same canonical name (the root and the bass are named by their pitch, not by their spelling).
    /// </summary>
    public string ToCanonical()
        => $"{NoteConstants.CanonicalNames[Root.Value]}{ChordType.ToCanonical()}{(Bass.HasValue ? $"/{NoteConstants.CanonicalNames[Bass.Value.Value]}" : null)}";
}
