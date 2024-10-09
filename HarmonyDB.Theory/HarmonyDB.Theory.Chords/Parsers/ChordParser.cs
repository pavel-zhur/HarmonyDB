using System.Text.RegularExpressions;
using HarmonyDB.Theory.Chords.Constants;
using HarmonyDB.Theory.Chords.Models;
using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using OneShelf.Common;

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

        var trace = new ChordParseTrace();

        if (stringRepresentation == string.Empty)
            return new(ChordParseError.EmptyString, trace);

        if (ChordConstants.NoChordVariants.Contains(stringRepresentation))
            return new(ChordParseResultType.SpecialNoChord);

        var (bassRepresentation, fret) = ExtractBassAndFret(ref stringRepresentation, out var bass, options);
        trace.BassRepresentation = bassRepresentation;
        trace.Fret = fret;

        if (options.ForgiveEdgeWhitespaces)
            stringRepresentation = stringRepresentation.Trim();

        if (stringRepresentation == string.Empty)
            return new(ChordParseError.EmptyString, trace);

        var prefixLength = NoteParser.TryParsePrefixNote(stringRepresentation, out var root, out var rootRepresentation, options.NoteParsingOptions);
        if (prefixLength == 0)
            return new(ChordParseError.UnreadableRoot, trace);

        var chordTypeRepresentation = stringRepresentation.Substring(prefixLength);
        trace.ChordTypeRepresentation = chordTypeRepresentation;

        if (bass.HasValue && root.Equals(bass.Value))
        {
            if (options.ForgiveSameBass)
                bassRepresentation = null;
            else
                return new(ChordParseError.SameBass, trace);
        }

        var (tokens, error) = TryGetTokens(chordTypeRepresentation, options.ChordTypeParsingOptions);
        if (error.HasValue)
            return new(error.Value, trace);

        trace.ChordTypeTokens = tokens;

        (var chordType, error, var logic, var branchIndex) = TryInterpretChordType(tokens!, fret, options.ChordTypeParsingOptions);
        if (error.HasValue)
            return new(error.Value, trace);

        trace.ChordTypeParseLogic = logic;
        trace.ChordTypeParseBranchIndex = branchIndex;

        return new(new ChordRepresentation(rootRepresentation, bassRepresentation, chordType!), trace);
    }

    internal static (ChordType? chordType, ChordParseError? error, ChordTypeParseLogic? logic, byte branchIndex) TryInterpretChordType(
        IReadOnlyList<(ChordTypeToken token, bool fromParentheses, MatchAmbiguity matchAmbiguity)> readonlyTokens,
        byte? bassFret,
        ChordTypeParsingOptions options)
    {
        var tokens = readonlyTokens.ToList();

        void ReplaceWithASeparator(Func<ChordTypeToken, bool> selector)
            => ReplaceWithASeparator2(x => selector(x.token));

        void ReplaceWithASeparator2(Func<(ChordTypeToken token, bool fromParentheses, MatchAmbiguity matchAmbiguity), bool> selector)
        {
            tokens = tokens
                .Select(x => selector(x) ? (new(ChordTypeMeaninglessAddition.FragmentSeparator), false, MatchAmbiguity.Safe) : x)
                .ToList();
        }

        #region Removing stars, apostrophes, leading slashes, handling questions. No Star, Apostrophe, Question after this point. Possible middle slashes and separators.

        {
            switch (options.QuestionsParsingBehavior)
            {
                case QuestionsParsingBehavior.IgnoreAndTreatOnlyAsPower:
                {
                    var only = tokens.All(x => x.token.MeaninglessAddition == ChordTypeMeaninglessAddition.Question);
                    if (only)
                    {
                        if (tokens.RemoveAll(x => x.token.MeaninglessAddition == ChordTypeMeaninglessAddition.Question) > 0)
                        {
                            tokens.Add((new(ChordMainType.Power), false, MatchAmbiguity.Safe));
                        }
                    }
                    else
                    {
                        ReplaceWithASeparator(x => x.MeaninglessAddition == ChordTypeMeaninglessAddition.Question);
                    }

                    break;
                }

                case QuestionsParsingBehavior.Ignore:
                    ReplaceWithASeparator(x => x.MeaninglessAddition == ChordTypeMeaninglessAddition.Question);
                    break;
            }

            var newCount = tokens.Count;
            for (var i = tokens.Count - 1; i >= 0; i--)
            {
                if (tokens[i].token.MeaninglessAddition switch
                    {
                        ChordTypeMeaninglessAddition.Star => options.IgnoreTrailingStars,
                        ChordTypeMeaninglessAddition.Question => options.QuestionsParsingBehavior > QuestionsParsingBehavior.Error,
                        ChordTypeMeaninglessAddition.FragmentSeparator => true,
                        ChordTypeMeaninglessAddition.Slash => options.IgnoreTrailingSlashes,
                        ChordTypeMeaninglessAddition.Apostrophe => options.IgnoreTrailingApostrophes,
                        null => false,
                        _ => throw new ArgumentOutOfRangeException(),
                    })
                {
                    newCount = i;
                }
                else
                {
                    break;
                }
            }

            if (newCount < tokens.Count)
                tokens = tokens.Take(newCount).ToList();

            if (tokens.Any(x => x.token.MeaninglessAddition is ChordTypeMeaninglessAddition.Star
                    or ChordTypeMeaninglessAddition.Apostrophe or ChordTypeMeaninglessAddition.Question))
            {
                return (null, ChordParseError.BadSymbols, null, 0);
            }
        }

        #endregion

        var allAdditions = ChordTypeAdditions.None;
        #region Gathering all additions, checking uniqueness

        {
            foreach (var addition in tokens.Where(x => x.token.Addition.HasValue).GroupBy(x => x.token.Addition!.Value))
            {
                if (addition.Count() > 1)
                    return (null, ChordParseError.DuplicateAdditions, null, 1);

                allAdditions |= addition.Key;
            }
        }

        #endregion

        #region Extensions tokens unique of each extension type

        {
            if (tokens.Where(x => x.token.Extension.HasValue).GroupBy(x => x.token.Extension!.Value)
                .Any(g => g.Count() > 1))
                return (null, ChordParseError.EachExtensionTypeExpectedUnique, null, 2);
        }

        #endregion

        #region Max one maj extension token

        {
            if (tokens.Count(x => x.token.Extension >= ChordTypeExtension.XMaj7) > 1)
                return (null, ChordParseError.MaxOneMajExtensionExpected, null, 3);
        }

        #endregion

        return (null, ChordParseError.NotImplemented, null, 255);
    }

    internal static (List<(ChordTypeToken token, bool fromParentheses, MatchAmbiguity matchAmbiguity)>? tokens, ChordParseError? error) TryGetTokens(string input, ChordTypeParsingOptions options)
    {
        var (fragments, error) = TryUnwrapParentheses(input, options);
        if (error.HasValue)
            return (null, error);

        List<(ChordTypeToken token, bool fromParentheses, MatchAmbiguity matchAmbiguity)> result = new();

        foreach (var ((fragment, fromParentheses), i) in fragments!.WithIndices())
        {
            if (i > 0)
            {
                result.Add((new(ChordTypeMeaninglessAddition.FragmentSeparator), false, MatchAmbiguity.Safe));
            }

            var currentFragment = fragment;

            while (true)
            {
                var match = ChordConstants.AllRepresentations
                    // ReSharper disable AccessToModifiedClosure
                    .FirstOrNull(x => currentFragment.Length >= x.representation.Length && x.matchCase switch
                    {
                        MatchCase.ExactOnly => currentFragment.StartsWith(x.representation),
                        
                        MatchCase.MatchUpperFirst => (currentFragment[0] == x.representation[0] || char.ToLowerInvariant(currentFragment[0]) == x.representation[0])
                                                     && currentFragment[1..x.representation.Length] == x.representation[1..],
                        
                        MatchCase.MatchUpperFirstOrAll => currentFragment.StartsWith(x.representation) 
                                                          || char.ToLowerInvariant(currentFragment[0]) == x.representation[0] && currentFragment[1..x.representation.Length] == x.representation[1..]
                                                          || currentFragment.StartsWith(x.representation.ToUpperInvariant()),
                        
                        MatchCase.MatchAny => currentFragment.StartsWith(x.representation, StringComparison.InvariantCultureIgnoreCase),
                        
                        _ => throw new ArgumentOutOfRangeException(),
                    });
                // ReSharper restore AccessToModifiedClosure

                if (!match.HasValue)
                {
                    return (null, ChordParseError.UnexpectedChordTypeToken);
                }

                result.Add((match.Value.token, fromParentheses, match.Value.matchAmbiguity));
                if (currentFragment.Length == match.Value.representation.Length)
                {
                    break;
                }

                currentFragment = currentFragment[match.Value.representation.Length..];
            }
        }

        return (result, null);
    }

    internal static (List<(string fragment, bool fromParentheses)>? fragments, ChordParseError? error) TryUnwrapParentheses(string input, ChordTypeParsingOptions options)
    {
        var match = Regex.Match(input, "^([^\\(\\)]*)\\(([^\\(\\)]+)\\)([^\\(\\)]*)$");

        List<(string input, bool fromParentheses)> inputs;
        if (!match.Success)
        {
            inputs = [(input, false)];
        }
        else
        {
            inputs = match.Groups[2].Value.Split(',').Select(x => (x, true))
                .Prepend((match.Groups[1].Value, false))
                .Append((match.Groups[3].Value, false))
                .ToList();
        }

        inputs = inputs.Where(x => x.input != string.Empty).ToList();

        var trimmed = inputs.Select(x => x with { input = x.input.Trim() }).Where(x => x.input != string.Empty).ToList();
        if (!options.TrimWhitespaceFragments && (trimmed.Count != inputs.Count || trimmed.Zip(inputs).Any(x => x.First != x.Second)))
        {
            return new(null, ChordParseError.WhitespaceFragments);
        }

        return (trimmed, null);
    }

    internal static (NoteRepresentation? bass, byte? fret) ExtractBassAndFret(ref string stringRepresentation, out Note? bass, ChordParsingOptions options)
    {
        var addition = string.Empty;

        void Add(ChordTypeMeaninglessAddition additionType)
            => addition += string.Join(
                string.Empty,
                ChordConstants.ChordTypeMeaninglessAdditionRepresentations
                    .Where(x => x.addition == additionType)
                    .Select(x => $"\\{x.representation}"));

        if (options.ChordTypeParsingOptions.IgnoreTrailingApostrophes) 
            Add(ChordTypeMeaninglessAddition.Apostrophe);

        if (options.ChordTypeParsingOptions.IgnoreTrailingStars) 
            Add(ChordTypeMeaninglessAddition.Star);

        if (options.ChordTypeParsingOptions.QuestionsParsingBehavior > QuestionsParsingBehavior.Error) 
            Add(ChordTypeMeaninglessAddition.Question);

        var bassAddition = addition;
        var nonBassAddition = addition;
        if (addition.Length > 0)
        {
            bassAddition = $"[{bassAddition}]*";
            nonBassAddition = $"[^{nonBassAddition}]+";
        }
        else
        {
            nonBassAddition = ".+";
        }

        if (options.ChordTypeParsingOptions.IgnoreTrailingSlashes)
            Add(ChordTypeMeaninglessAddition.Slash);

        if (addition.Length > 0) addition = $"[{addition}]*";

        byte? fret = null;
        var fretMatch = Regex.Match(stringRepresentation, @$"^(.*)\(([XIV]+)\){addition}$");
        if (fretMatch.Success && ChordConstants.Romans.WithIndicesNullable().SingleOrDefault(x => x.x == fretMatch.Groups[2].Value).i is { } i)
        {
            fret = (byte)(i + 1);
            stringRepresentation = fretMatch.Groups[1].Value;
        }

        var bassMatch = Regex.Match(stringRepresentation, $@"^(.*)[/\\]({nonBassAddition}){bassAddition}$");

        if (bassMatch.Success && NoteParser.TryParseNote(bassMatch.Groups[2].Value, out var note, out var bassRepresentation, options.NoteParsingOptions))
        {
            bass = note;
            stringRepresentation = bassMatch.Groups[1].Value;
        }
        else
        {
            bass = null;
            bassRepresentation = null;
        }
        
        return (bassRepresentation, fret);
    }
}