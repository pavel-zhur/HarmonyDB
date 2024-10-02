using HarmonyDB.Theory.Chords.Models;
using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;

namespace HarmonyDB.Theory.Chords.Parsers;

public static class ChordParser
{
    public static ChordParseResult Parse(string stringRepresentation, ChordParsingOptions? options = null)
    {
        options ??= ChordParsingOptions.Default;

        if (options.ForgiveEdgeWhitespaces)
            stringRepresentation = stringRepresentation.Trim();

        if (options.ForgiveRoundBraces && stringRepresentation.Length > 0 && stringRepresentation[0] == '(' && stringRepresentation[^1] == ')')
            stringRepresentation = stringRepresentation.Substring(1, stringRepresentation.Length - 1);

        if (NoteConstants.NoChordVariants.Contains(stringRepresentation))
            return new(ChordParseResultType.SpecialNoChord);

        var bassRepresentation = ExtractBass(ref stringRepresentation, options);

        var prefixLength = NoteParser.TryParsePrefixNote(stringRepresentation, out var root, out var rootRepresentation, options.NoteParsingOptions);
        if (prefixLength == 0)
            throw new ArgumentOutOfRangeException(nameof(stringRepresentation), stringRepresentation, "The chord root could not be parsed.");

        var chord = stringRepresentation.Substring(prefixLength);

        return new(new Chord(rootRepresentation, bassRepresentation, chord));
    }

    private static NoteRepresentation? ExtractBass(ref string stringRepresentation, ChordParsingOptions options)
    {
        var slashParts = stringRepresentation.Split('/');
        NoteRepresentation? bassRepresentation = null;

        switch (slashParts.Length)
        {
            case 0:
                throw new ArgumentOutOfRangeException(nameof(stringRepresentation), stringRepresentation, "An empty string could not be parsed.");

            case > 1:
                if (NoteParser.TryParseNote(slashParts[^1], out var bass, out bassRepresentation, options.NoteParsingOptions))
                    stringRepresentation = string.Join(string.Empty, slashParts.SkipLast(1));
                break;
        }

        return bassRepresentation;
    }
}