namespace HarmonyDB.Theory.Chords.Models;

public readonly record struct Note
{
    public Note(byte value)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (value < NoteConstants.MinNoteValue || value > NoteConstants.MaxNoteValue)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"A value between {NoteConstants.MinNoteValue} and {NoteConstants.MaxNoteValue} (inclusive) is expected.");

        Value = value;
    }

    public byte Value { get; }

    public static Note Normalized(int value) => new(Normalize(value));

    public static byte Normalize(int notes) => (byte)((notes % NoteConstants.TotalNotes + NoteConstants.TotalNotes) % NoteConstants.TotalNotes);
}