using HarmonyDB.Theory.Chords.Models;
using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using OneShelf.Common;

namespace HarmonyDB.Theory.Chords.Parsers;

public static class NoteParser
{
    public static int TryParsePrefixNote(string noteRepresentation, out Note note, out NoteRepresentation result, NoteParsingOptions? options = null)
    {
        result = new(default, default, default);
        note = default;

        if (string.IsNullOrWhiteSpace(noteRepresentation))
            return 0;

        int toSkip;
        NaturalNoteRepresentation naturalNoteRepresentation;
        var solfegeMatch = NoteConstants.NaturalNoteSolfegeNames.WithIndicesNullable().FirstOrDefault(x => noteRepresentation.StartsWith(x.x)).i;
        if (solfegeMatch.HasValue)
        {
            toSkip = NoteConstants.NaturalNoteSolfegeNames[solfegeMatch.Value].Length;
            naturalNoteRepresentation = NaturalNoteRepresentation.La + (sbyte)solfegeMatch;
        }
        else if (!TryParseNaturalNote(noteRepresentation[0], out note, out naturalNoteRepresentation, options))
            return 0;
        else
        {
            toSkip = 1;
        }

        var sharps = 0;
        var flats = 0;
        foreach (var @char in noteRepresentation.Skip(toSkip))
        {
            if (!TryParseAlteration(@char, out var alteration))
                break;

            switch (alteration)
            {
                case Alteration.Sharp:
                    if (flats > 0) break;
                    sharps++;
                    break;
                case Alteration.Flat:
                    if (sharps > 0) break;
                    flats++;
                    break;
                default:
                    throw new($"Unexpected alteration: {@char}.");
            }
        }

        result = new(naturalNoteRepresentation, sharps, flats);
        note = Note.Normalized(note.Value + sharps - flats);
        return toSkip + sharps + flats;
    }

    public static int TryParsePrefixNote(string noteRepresentation, out Note note, NoteParsingOptions? options = null)
        => TryParsePrefixNote(noteRepresentation, out note, out _, options);

    public static bool TryParseNote(string noteRepresentation, out Note note, out NoteRepresentation result, NoteParsingOptions? options = null)
        => TryParsePrefixNote(noteRepresentation, out note, out result, options) == noteRepresentation.Length;

    public static bool TryParseNote(string noteRepresentation, out Note note, NoteParsingOptions? options = null)
        => TryParseNote(noteRepresentation, out note, out _, options);

    public static Note ParseNote(string noteRepresentation, NoteParsingOptions? options = null)
        => TryParseNote(noteRepresentation, out var result)
            ? result
            : throw new ArgumentOutOfRangeException(nameof(noteRepresentation), noteRepresentation, "The note could not be parsed.");

    public static bool TryParseAlteration(char character, out Alteration alteration)
    {
        if (NoteConstants.FlatSymbols.Contains(character))
        {
            alteration = Alteration.Flat;
            return true;
        }

        if (NoteConstants.SharpSymbols.Contains(character))
        {
            alteration = Alteration.Sharp;
            return true;
        }

        alteration = Alteration.Flat;
        return false;
    }

    public static Alteration ParseAlteration(char character)
        => TryParseAlteration(character, out var result)
            ? result
            : throw new ArgumentOutOfRangeException(nameof(character), character, "The alteration could not be parsed.");

    public static bool TryParseNaturalNote(char character, out Note note, out NaturalNoteRepresentation naturalNoteRepresentation, NoteParsingOptions? options = null)
    {
        options ??= NoteParsingOptions.Default;

        if (!options.CaseSensitive)
            character = char.ToUpperInvariant(character);

        if (options.ForgiveRussianC && character == 'С')
            character = 'C';

        note = default;
        naturalNoteRepresentation = default;
        if (character < NoteConstants.MinNaturalNoteChar || character > NoteConstants.MaxNaturalNoteChar)
        {
            if (character == NoteConstants.HSynonymChar)
            {
                if (options.HHandling == HHandling.HProhibited)
                    return false;

                naturalNoteRepresentation = NaturalNoteRepresentation.H;
                note = new((byte)NoteConstants.HSynonym);
                return true;
            }

            return false;
        }

        var naturalNote = NoteConstants.NaturalNotes[(byte)(character - NoteConstants.MinNaturalNoteChar)];
        note = new((byte)naturalNote);
        naturalNoteRepresentation = (NaturalNoteRepresentation)naturalNote;

        if (note.Value == (byte)NaturalNote.B && options.HHandling == HHandling.BbMeansH)
        {
            note = NoteConstants.GermanBNote;
            naturalNoteRepresentation = NaturalNoteRepresentation.GermanB;
        }

        return true;
    }

    public static bool TryParseNaturalNote(char character, out Note note, NoteParsingOptions? options = null)
        => TryParseNaturalNote(character, out note, out _, options);

    public static Note ParseNaturalNote(char character, NoteParsingOptions? options = null)
    {
        options ??= NoteParsingOptions.Default;

        return TryParseNaturalNote(character, out var result, options)
            ? result
            : throw new ArgumentOutOfRangeException(nameof(character), character,
                $"A value between {NoteConstants.MinNaturalNoteChar} and {NoteConstants.MaxNaturalNoteChar} (inclusive){(options.HHandling == HHandling.HProhibited ? null : $" or {NoteConstants.HSynonymChar}")} is expected.");
    }
}