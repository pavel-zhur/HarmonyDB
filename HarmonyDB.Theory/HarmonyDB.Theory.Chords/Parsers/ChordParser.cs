using HarmonyDB.Theory.Chords.Constants;
using HarmonyDB.Theory.Chords.Models;
using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using OneShelf.Common;

namespace HarmonyDB.Theory.Chords.Parsers;

public static class ChordParser
{
    public static List<(ChordType? type, ChordTypeExtension? extension, ChordTypeAdditions? addition, byte? fret, ChordTypeMeaninglessAddition? meaninglessAddition, bool fromParentheses, MatchAmbiguity matchAmbiguity)>? TryGetTokens(string input, ChordTypeParsingOptions options)
    {
        var fragments = UnwrapParentheses(input, options);
        List<(ChordType? type, ChordTypeExtension? extension, ChordTypeAdditions? addition, byte? fret, ChordTypeMeaninglessAddition? meaninglessAddition, bool fromParentheses, MatchAmbiguity matchAmbiguity)> result = new();

        foreach (var (fragment, fromParentheses) in fragments)
        {
            var currentFragment = fragment;

            while (true)
            {
                var match = ChordConstants.AllRepresentations
                    // ReSharper disable AccessToModifiedClosure
                    .FirstOrNull(x => currentFragment.Length >= x.representation.Length && x.matchCase switch
                        {
                        MatchCase.ExactOnly => currentFragment.StartsWith(x.representation),
                        MatchCase.MatchUpperFirst => currentFragment.StartsWith(x.representation)
                                                     || char.ToLowerInvariant(currentFragment[0]) == x.representation[0] && currentFragment[1..x.representation.Length] == x.representation[1..],
                        MatchCase.MatchUpperFirstOrAll => currentFragment.StartsWith(x.representation)
                                                          || char.ToLowerInvariant(currentFragment[0]) == x.representation[0] && currentFragment[1..x.representation.Length] == x.representation[1..]
                                                          || currentFragment.ToLowerInvariant() == x.representation,
                        _ => throw new ArgumentOutOfRangeException(),
                    });
                // ReSharper restore AccessToModifiedClosure

                if (!match.HasValue)
                {
                    return null;
                }

                result.Add((match.Value.type, match.Value.extension, match.Value.addition, match.Value.fret, match.Value.meaninglessAddition, fromParentheses, match.Value.matchAmbiguity));
                if (currentFragment.Length == match.Value.representation.Length)
                {
                    break;
                }

                currentFragment = currentFragment[match.Value.representation.Length..];
            }
        }

        return result;
    }

    internal static List<(string fragment, bool fromParentheses)> UnwrapParentheses(string input, ChordTypeParsingOptions options)
    {
        if (input == string.Empty) return new();

        var inputs = (input, fromParentheses: false).Once().ToList();

        if (input.EndsWith(')') && input.Count(x => x == '(') == 1 && input.Count(x => x == ')') == 1)
        {
            inputs = (input[..input.IndexOf('(')], false)
                .Once()
                .Concat(input[(input.IndexOf('(') + 1)..^1]
                    .Split(',')
                    .Select(x => (x, true)))
                .ToList();
        }

        var trimmed = inputs.Select(x => x with { input = x.input.Trim() }).Where(x => x.input != string.Empty).ToList();
        if (!options.TrimWhitespaceFragments && (trimmed.Count != inputs.Count || trimmed.Zip(inputs).Any(x => x.First != x.Second)))
        {
            throw new ArgumentOutOfRangeException(nameof(input), input, "Whitespace fragments.");
        }

        return trimmed;
    }

    public static ChordParseResult Parse(string stringRepresentation, ChordParsingOptions? options = null)
    {
        options ??= ChordParsingOptions.Default;

        if (options.ForgiveEdgeWhitespaces)
            stringRepresentation = stringRepresentation.Trim();

        if (options.ForgiveRoundBraces && stringRepresentation.Length > 0 && stringRepresentation[0] == '(' && stringRepresentation[^1] == ')')
            stringRepresentation = stringRepresentation.Substring(1, stringRepresentation.Length - 1);

        if (ChordConstants.NoChordVariants.Contains(stringRepresentation))
            return new(ChordParseResultType.SpecialNoChord);

        var bassRepresentation = ExtractBass(ref stringRepresentation, out var bass, options);

        var prefixLength = NoteParser.TryParsePrefixNote(stringRepresentation, out var root, out var rootRepresentation, options.NoteParsingOptions);
        if (prefixLength == 0)
            throw new ArgumentOutOfRangeException(nameof(stringRepresentation), stringRepresentation, "The chord root could not be parsed.");

        var chord = stringRepresentation.Substring(prefixLength);

        if (bass.HasValue && root.Equals(bass.Value))
        {
            if (options.ForgiveSameBass)
                bassRepresentation = null;
            else
                throw new ArgumentOutOfRangeException(nameof(stringRepresentation), stringRepresentation, "The bass is the same as the root.");
        }

        return new(new Chord(rootRepresentation, bassRepresentation, chord));
    }

    private static NoteRepresentation? ExtractBass(ref string stringRepresentation, out Note? bass, ChordParsingOptions options)
    {
        var slashParts = stringRepresentation.Split('/');
        NoteRepresentation? bassRepresentation = null;
        bass = null;

        switch (slashParts.Length)
        {
            case 0:
                throw new ArgumentOutOfRangeException(nameof(stringRepresentation), stringRepresentation, "An empty string could not be parsed.");

            case > 1:
                if (NoteParser.TryParseNote(slashParts[^1], out var note, out bassRepresentation, options.NoteParsingOptions))
                {
                    stringRepresentation = string.Join(string.Empty, slashParts.SkipLast(1));
                    bass = note;
                }

                break;
        }

        return bassRepresentation;
    }
}