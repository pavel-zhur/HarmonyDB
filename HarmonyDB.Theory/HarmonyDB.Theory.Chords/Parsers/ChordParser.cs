using System.Text.RegularExpressions;
using HarmonyDB.Theory.Chords.Constants;
using HarmonyDB.Theory.Chords.Models;
using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using OneShelf.Common;

namespace HarmonyDB.Theory.Chords.Parsers;

public static partial class ChordParser
{
    public static ChordParseResult Parse(string stringRepresentation, ChordParsingOptions? options = null)
    {
        options ??= ChordParsingOptions.Default;

        if (options.NeedsSong)
            throw new InvalidOperationException("The options leave some meanings to be detected from the song: use ParseSong with the song's chords.");

        if (options.ForgiveEdgeWhitespaces)
            stringRepresentation = stringRepresentation.Trim();

        if (options.NoteParsingOptions.ForgiveCyrillicLetters)
            stringRepresentation = stringRepresentation.Replace('м', 'm').Replace('о', 'o');

        // The chord in round braces: "(Am)"; or one of the chords written in braces together, split apart: "(F", "G)".
        if (options.ForgiveRoundBraces && stringRepresentation.Length > 1)
        {
            var opening = stringRepresentation.Count(c => c == '(');
            var closing = stringRepresentation.Count(c => c == ')');
            if (stringRepresentation[0] == '(' && stringRepresentation[^1] == ')')
                stringRepresentation = stringRepresentation.Substring(1, stringRepresentation.Length - 2);
            else if (stringRepresentation[0] == '(' && opening == closing + 1)
                stringRepresentation = stringRepresentation.Substring(1);
            else if (stringRepresentation[^1] == ')' && closing == opening + 1)
                stringRepresentation = stringRepresentation.Substring(0, stringRepresentation.Length - 1);
        }

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

        // The root is read as a solfège name first (when enabled); if the rest is unreadable then, the letter reading is tried ("Fadd9").
        ChordParseResult? firstFailure = null;
        foreach (var lettersOnly in new[] { false, true })
        {
            var attemptTrace = trace with { };

            var prefixLength = NoteParser.TryParsePrefixNote(stringRepresentation, out _, out var rootRepresentation, options.NoteParsingOptions, lettersOnly);
            if (prefixLength == 0)
                return firstFailure ?? new(ChordParseError.UnreadableRoot, attemptTrace);

            var chordTypeRepresentation = stringRepresentation.Substring(prefixLength);

            // In the German notation a lower case root is the minor chord: "fis", "a7".
            var germanMinor = options.NoteParsingOptions.GermanNoteNames == NotationUse.Always
                              && stringRepresentation[0] is >= 'a' and <= 'h'
                              && rootRepresentation.NaturalNoteRepresentation is not (>= NaturalNoteRepresentation.La and <= NaturalNoteRepresentation.Sol);
            if (germanMinor && !chordTypeRepresentation.StartsWith('m'))
                chordTypeRepresentation = "m" + chordTypeRepresentation;

            var result = TryParseAfterRoot(chordTypeRepresentation, rootRepresentation, bassRepresentation, bass, attemptTrace, options);
            if (result.Type == ChordParseResultType.Success)
            {
                if (lettersOnly)
                    attemptTrace.ChordTypeParseLogic |= ChordTypeParseLogic.LetterRootInsteadOfSolfege;

                return result;
            }

            firstFailure ??= result;

            if (rootRepresentation.NaturalNoteRepresentation is < NaturalNoteRepresentation.La or > NaturalNoteRepresentation.Sol)
                break;
        }

        return firstFailure!;
    }

    private static ChordParseResult TryParseAfterRoot(
        string chordTypeRepresentation,
        NoteRepresentation rootRepresentation,
        NoteRepresentation? bassRepresentation,
        Note? bass,
        ChordParseTrace trace,
        ChordParsingOptions options)
    {
        trace.ChordTypeRepresentation = chordTypeRepresentation;

        if (options.ChordTypeParsingOptions.ForgiveSwallowedAdd && chordTypeRepresentation.StartsWith("dd"))
            chordTypeRepresentation = "a" + chordTypeRepresentation;

        var (tokens, error) = TryGetTokens(chordTypeRepresentation, options.ChordTypeParsingOptions);
        if (error.HasValue)
            return new(error.Value, trace);

        trace.ChordTypeTokens = tokens;

        (var chordType, var rootShift, error, var logic, var branchIndex) = TryInterpretChordType(tokens!, options.ChordTypeParsingOptions);
        trace.ChordTypeParseLogic = logic;
        trace.ChordTypeParseBranchIndex = branchIndex;
        if (error.HasValue)
            return new(error.Value, trace);

        if (rootShift != 0)
            rootRepresentation = rootShift > 0
                ? rootRepresentation with { Sharps = rootRepresentation.Sharps + 1 }
                : rootRepresentation with { Flats = rootRepresentation.Flats + 1 };

        if (bass.HasValue && rootRepresentation.Note.Equals(bass.Value))
        {
            if (options.ForgiveSameBass)
                bassRepresentation = null;
            else
                return new(ChordParseError.SameBass, trace);
        }

        return new(new ChordRepresentation(rootRepresentation, bassRepresentation, chordType!), trace);
    }

    private enum AlterationBinding : byte
    {
        BeforeDegree,
        AfterDegree,
        Standalone,
        Ignored,
    }

    private enum DegreeKind : byte
    {
        /// <summary>
        /// A bare number: "7", "9", "6", "5".
        /// </summary>
        Bare,

        /// <summary>
        /// An added degree: "add9", "add 9", "add7".
        /// </summary>
        Added,

        /// <summary>
        /// A major extension: "maj7", "maj9", "M", "7M".
        /// </summary>
        Major,
    }

    private static bool IsAlteration(ChordTypeToken token)
        => token.AmbiguousAddition is ChordTypeAmbiguousAddition.Plus or ChordTypeAmbiguousAddition.Minus or ChordTypeAmbiguousAddition.Sharp or ChordTypeAmbiguousAddition.Flat;

    private static bool IsRaising(ChordTypeToken token)
        => token.AmbiguousAddition is ChordTypeAmbiguousAddition.Plus or ChordTypeAmbiguousAddition.Sharp;

    private static byte? GetDegree((ChordTypeToken token, bool fromParentheses, MatchAmbiguity matchAmbiguity) x)
        => x.token switch
        {
            { Extension: ChordTypeExtension.X7 or ChordTypeExtension.XMaj7 } => 7,
            { Extension: ChordTypeExtension.X9 or ChordTypeExtension.XMaj9 } => 9,
            { Extension: ChordTypeExtension.X11 or ChordTypeExtension.XMaj11 } => 11,
            { Extension: ChordTypeExtension.X13 or ChordTypeExtension.XMaj13 } => 13,
            { Addition: ChordTypeAdditions.Add2 } => 2,
            { Addition: ChordTypeAdditions.Add4 } => 4,
            { Addition: ChordTypeAdditions.Add6 } => 6,
            { Addition: ChordTypeAdditions.Add9 } => 9,
            { Addition: ChordTypeAdditions.Add11 } => 11,
            { Addition: ChordTypeAdditions.Add13 } => 13,
            { Type: ChordMainType.Power } when x.matchAmbiguity == MatchAmbiguity.AlterableInt => 5,
            _ => null,
        };

    private static ChordTypeAdditions AddedUpperDegree(byte degree)
        => degree switch
        {
            9 => ChordTypeAdditions.Add9,
            11 => ChordTypeAdditions.Add11,
            _ => ChordTypeAdditions.Add13,
        };

    internal static (ChordType? chordType, int rootShift, ChordParseError? error, ChordTypeParseLogic logic, byte branchIndex) TryInterpretChordType(
        IReadOnlyList<(ChordTypeToken token, bool fromParentheses, MatchAmbiguity matchAmbiguity)> readonlyTokens,
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

        #region Removing stars, apostrophes, trailing slashes, handling questions. No Star, Apostrophe, Question after this point. Possible middle slashes and separators.

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
                        ChordTypeMeaninglessAddition.Punctuation => options.IgnoreTrailingPunctuation,
                        ChordTypeMeaninglessAddition.VariantNumber => options.IgnoreVariantNumbers,
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
                    or ChordTypeMeaninglessAddition.Apostrophe or ChordTypeMeaninglessAddition.Question or ChordTypeMeaninglessAddition.Punctuation or ChordTypeMeaninglessAddition.VariantNumber))
            {
                return (null, 0, ChordParseError.BadSymbols, ChordTypeParseLogic.None, 0);
            }
        }

        #endregion

        #region Additions tokens unique

        {
            if (tokens.Where(x => x.token.Addition.HasValue).GroupBy(x => x.token.Addition!.Value).Any(g => g.Count() > 1))
                return (null, 0, ChordParseError.DuplicateAdditions, ChordTypeParseLogic.None, 1);
        }

        #endregion

        #region Extensions tokens unique of each extension type

        {
            if (tokens.Where(x => x.token.Extension.HasValue).GroupBy(x => x.token.Extension!.Value)
                .Any(g => g.Count() > 1))
                return (null, 0, ChordParseError.EachExtensionTypeExpectedUnique, ChordTypeParseLogic.None, 2);
        }

        #endregion

        #region Max one maj extension token

        {
            if (tokens.Count(x => x.token.Extension >= ChordTypeExtension.XMaj7) > 1)
                return (null, 0, ChordParseError.MaxOneMajExtensionExpected, ChordTypeParseLogic.None, 3);
        }

        #endregion

        #region Alterations: each sharp, flat, plus or minus alters the degree after it or before it, or stands alone. All the readings are tried, the cheapest valid one wins.

        {
            var alterations = tokens.WithIndices().Where(x => IsAlteration(x.x.token)).Select(x => x.i).ToList();
            if (alterations.Count > ChordConstants.MaxAlterations)
                return (null, 0, ChordParseError.TooManyAlterations, ChordTypeParseLogic.None, 4);

            // The pluses and minuses are written before or after the degree ("C7+5", "C75+").
            // The sharps and flats are written before it ("C7#5"), or after it when it stands apart in parentheses or after a slash ("C7/9b", "C7(9b)");
            // a sharp or flat right after the first degree is a misplaced root accidental rather ("F5#" is F#5).
            bool Separated(int i) => tokens[i].fromParentheses || tokens.Take(i).Any(x => x.token.MeaninglessAddition.HasValue);

            var choices = alterations
                .Select(i => new[]
                    {
                        (binding: AlterationBinding.BeforeDegree,
                            possible: i + 1 < tokens.Count && tokens[i + 1].matchAmbiguity == MatchAmbiguity.AlterableInt),
                        (binding: AlterationBinding.AfterDegree,
                            possible: i > 0
                                      && tokens[i - 1].matchAmbiguity is MatchAmbiguity.AlterableInt or MatchAmbiguity.AlterableDegree
                                      && (tokens[i].token.AmbiguousAddition is ChordTypeAmbiguousAddition.Plus or ChordTypeAmbiguousAddition.Minus
                                          || Separated(i - 1))),
                        (binding: AlterationBinding.Standalone, possible: true),

                        // A minus at the very end that alters nothing is a hold mark ("Em7-", "Aadd9-");
                        // with other minuses in the chord type it is a tab fragment rather ("D-7-", "E-5-5-").
                        (binding: AlterationBinding.Ignored,
                            possible: options.IgnoreTrailingPunctuation
                                      && i == tokens.Count - 1
                                      && i > 0
                                      && tokens[i].token.AmbiguousAddition == ChordTypeAmbiguousAddition.Minus
                                      && tokens.Count(x => x.token.AmbiguousAddition == ChordTypeAmbiguousAddition.Minus) == 1),
                    }
                    .Where(x => x.possible)
                    .Select(x => x.binding)
                    .ToList())
                .ToList();

            (ChordType? chordType, int rootShift, ChordParseError? error, ChordTypeParseLogic logic, byte branchIndex, int cost)? best = null;
            (ChordType? chordType, int rootShift, ChordParseError? error, ChordTypeParseLogic logic, byte branchIndex, int cost)? firstFailure = null;

            var assignment = new AlterationBinding[alterations.Count];

            void Enumerate(int k)
            {
                if (k == alterations.Count)
                {
                    var targets = assignment
                        .Select((b, j) => b switch
                        {
                            AlterationBinding.BeforeDegree => alterations[j] + 1,
                            AlterationBinding.AfterDegree => alterations[j] - 1,
                            _ => -1,
                        })
                        .Where(t => t >= 0)
                        .ToList();

                    if (targets.Distinct().Count() < targets.Count)
                        return;

                    var result = TryBuildChordType(tokens, alterations, assignment, options);
                    if (result.error.HasValue)
                    {
                        if (!firstFailure.HasValue || result.cost < firstFailure.Value.cost)
                            firstFailure = result;
                    }
                    else if (!best.HasValue || result.cost < best.Value.cost)
                    {
                        best = result;
                    }

                    return;
                }

                foreach (var binding in choices[k])
                {
                    assignment[k] = binding;
                    Enumerate(k + 1);
                }
            }

            Enumerate(0);

            var chosen = best ?? firstFailure!.Value;
            return (chosen.chordType, chosen.rootShift, chosen.error, chosen.logic, chosen.branchIndex);
        }

        #endregion
    }

    private static (ChordType? chordType, int rootShift, ChordParseError? error, ChordTypeParseLogic logic, byte branchIndex, int cost) TryBuildChordType(
        IReadOnlyList<(ChordTypeToken token, bool fromParentheses, MatchAmbiguity matchAmbiguity)> tokens,
        IReadOnlyList<int> alterations,
        IReadOnlyList<AlterationBinding> assignment,
        ChordTypeParsingOptions options)
    {
        var logic = ChordTypeParseLogic.None;
        var cost = 0;
        var rootShift = 0;
        var qualities = new HashSet<ChordMainType>();
        var additions = ChordTypeAdditions.None;
        var halfDiminished = false;

        (ChordType? chordType, int rootShift, ChordParseError? error, ChordTypeParseLogic logic, byte branchIndex, int cost) Fail(ChordParseError error, byte branchIndex)
            => (null, 0, error, logic, branchIndex, cost);

        #region Alterations: the bound ones go to their degrees, the standalone ones are read on their own.

        var boundAlterations = new Dictionary<int, ChordTypeAmbiguousAddition>();
        for (var k = 0; k < alterations.Count; k++)
        {
            var index = alterations[k];
            var token = tokens[index].token;
            switch (assignment[k])
            {
                case AlterationBinding.BeforeDegree:
                    boundAlterations[index + 1] = token.AmbiguousAddition!.Value;
                    logic |= ChordTypeParseLogic.AlterationBeforeDegree;
                    break;

                case AlterationBinding.Ignored:
                    logic |= ChordTypeParseLogic.TrailingMinusIgnored;
                    cost += 15;
                    break;

                case AlterationBinding.AfterDegree:
                    boundAlterations[index - 1] = token.AmbiguousAddition!.Value;
                    logic |= ChordTypeParseLogic.AlterationAfterDegree;
                    cost += 1;
                    break;

                case AlterationBinding.Standalone:
                    switch (token.AmbiguousAddition)
                    {
                        case ChordTypeAmbiguousAddition.Plus:
                            if (!qualities.Add(ChordMainType.Aug))
                                return Fail(ChordParseError.UnboundAlteration, 5);

                            logic |= ChordTypeParseLogic.PlusAsAugmented;
                            cost += 10;
                            break;

                        case ChordTypeAmbiguousAddition.Minus:
                            if (index != 0)
                                return Fail(ChordParseError.UnboundAlteration, 5);

                            qualities.Add(ChordMainType.Minor);
                            logic |= ChordTypeParseLogic.MinusAsMinor;
                            cost += 10;
                            break;

                        default:
                            // Only in the first part of the chord type, before any slash or parentheses: "Fm#", "F5#", but not "C7/9b".
                            if (!options.ForgiveMisplacedRootAccidental || rootShift != 0 || tokens.Take(index).Any(x => x.token.MeaninglessAddition.HasValue))
                                return Fail(ChordParseError.UnboundAlteration, 5);

                            rootShift = IsRaising(token) ? 1 : -1;
                            logic |= ChordTypeParseLogic.MisplacedRootAccidental;
                            cost += 10;
                            break;
                    }

                    break;
            }
        }

        #endregion

        #region Degrees and qualities from the tokens. "add" makes the following bare degree added.

        // A degree is separated when it is written in parentheses or after a slash: "C(9)", "C/9", "C6/9".
        var degrees = new List<(byte degree, DegreeKind kind, ChordTypeAmbiguousAddition? alteration, bool separated)>();
        var pendingAdd = false;
        var afterSeparator = false;
        for (var i = 0; i < tokens.Count; i++)
        {
            var (token, fromParentheses, matchAmbiguity) = tokens[i];

            if (IsAlteration(token))
                continue;

            // "add" may be separated from its degree: "Cadd(9)", "Eadd(#11)".
            if (token.MeaninglessAddition.HasValue)
            {
                afterSeparator = true;
                continue;
            }

            if (token.AmbiguousAddition == ChordTypeAmbiguousAddition.Add)
            {
                if (pendingAdd)
                    return Fail(ChordParseError.AddWithoutDegree, 6);

                pendingAdd = true;
                continue;
            }

            var degree = GetDegree(tokens[i]);
            if (degree.HasValue)
            {
                if (pendingAdd && matchAmbiguity != MatchAmbiguity.AlterableInt)
                    return Fail(ChordParseError.AddWithoutDegree, 6);

                var kind = token.Extension >= ChordTypeExtension.XMaj7
                    ? DegreeKind.Major
                    : matchAmbiguity == MatchAmbiguity.AlterableInt && !pendingAdd
                        ? DegreeKind.Bare
                        : DegreeKind.Added;

                pendingAdd = false;
                degrees.Add((degree.Value, kind, boundAlterations.TryGetValue(i, out var alteration) ? alteration : null, fromParentheses || afterSeparator));
                continue;
            }

            if (pendingAdd)
                return Fail(ChordParseError.AddWithoutDegree, 6);

            switch (token)
            {
                // The major sixth chord: "Cmaj6", "CM6".
                case { AmbiguousAddition: ChordTypeAmbiguousAddition.Maj or ChordTypeAmbiguousAddition.CapitalM }
                    when i + 1 < tokens.Count && tokens[i + 1].matchAmbiguity == MatchAmbiguity.AlterableInt && GetDegree(tokens[i + 1]) == 6:
                    qualities.Add(token.AmbiguousAddition == ChordTypeAmbiguousAddition.CapitalM && options.CapitalMWithoutDegreeMeaning == CapitalMWithoutDegreeMeaning.Minor
                        ? ChordMainType.Minor
                        : ChordMainType.Major);
                    break;

                case { AmbiguousAddition: ChordTypeAmbiguousAddition.Maj }:
                    logic |= ChordTypeParseLogic.MajWithoutDegree;
                    if (options.MajWithoutDegreeMeaning == MajWithoutDegreeMeaning.MajorSeventh)
                        degrees.Add((7, DegreeKind.Major, null, fromParentheses || afterSeparator));
                    else
                        qualities.Add(ChordMainType.Major);

                    break;

                case { AmbiguousAddition: ChordTypeAmbiguousAddition.CapitalM }:
                    logic |= ChordTypeParseLogic.CapitalMWithoutDegree;
                    switch (options.CapitalMWithoutDegreeMeaning)
                    {
                        case CapitalMWithoutDegreeMeaning.MajorSeventh:
                            degrees.Add((7, DegreeKind.Major, null, fromParentheses || afterSeparator));
                            break;
                        case CapitalMWithoutDegreeMeaning.Major:
                            qualities.Add(ChordMainType.Major);
                            break;
                        default:
                            qualities.Add(ChordMainType.Minor);
                            break;
                    }

                    break;

                case { Type: { } type }:
                    qualities.Add(type);
                    break;

                case { AmbiguousAddition: ChordTypeAmbiguousAddition.HalfDiminished7 }:
                    halfDiminished = true;
                    break;

                case { AmbiguousAddition: ChordTypeAmbiguousAddition.Add9And11 }:
                    additions |= ChordTypeAdditions.Add9 | ChordTypeAdditions.Add11;
                    break;

                case { Addition: { } addition }:
                    additions |= addition;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(tokens), token, "Unexpected chord type token.");
            }
        }

        // "add" at the end, without its degree: it adds the bare degree written right before it ("C9add" is Cadd9), or the ninth ("Cadd" is Cadd9).
        if (pendingAdd)
        {
            logic |= ChordTypeParseLogic.AddWithoutDegree;
            var last = degrees.Count - 1;
            if (last >= 0 && degrees[last] is { kind: DegreeKind.Bare, alteration: null, degree: 2 or 4 or 6 or 9 or 11 or 13 }
                && GetDegree(tokens[^1]) == null && tokens[^1].token.AmbiguousAddition == ChordTypeAmbiguousAddition.Add)
                degrees[last] = degrees[last] with { kind = DegreeKind.Added };
            else
                degrees.Add((9, DegreeKind.Added, null, false));
        }

        // A degree is written once the same way: altered differently it may repeat ("C7(b9,#9)"),
        // and the seventh may be written both plain and as major ("C7maj", "C7M7").
        if (degrees
            .GroupBy(x => (x.degree, raised: x.alteration.HasValue ? IsRaising(new(x.alteration)) : (bool?)null))
            .Any(g => g.Count() > 1 && (g.Key.degree != 7 || g.Count() > 2 || g.Count(x => x.kind == DegreeKind.Major) != 1)))
            return Fail(ChordParseError.DuplicateDegrees, 6);

        #endregion

        #region Degrees: the extension (the seventh and the upper degrees), the added and altered degrees.

        var extensionDegrees = new SortedSet<byte>();
        var majorSeventh = false;
        var alteredBareNinth = false;
        var bareUpper = new List<(byte degree, bool separated)>();
        bool bareSecond = false, bareFourth = false, bareFifth = false, bareFifthSeparated = false;

        // The fifth written first names the power chord: "E5(7)", "D5maj7" have no third; written after the seventh it is just the fifth: "G#75".
        var bareFifthFirst = degrees.FirstOrDefault() is { degree: 5, kind: DegreeKind.Bare, alteration: null };

        foreach (var (degree, kind, alteration, separated) in degrees)
        {
            if (alteration.HasValue)
            {
                if (kind == DegreeKind.Major)
                    return Fail(ChordParseError.UnsupportedAlteration, 7);

                // Only the plus raises the seventh ("C7+", "C+7"); a sharp before the seven is not this notation ("Fm#7" is F#m7).
                if (degree == 7)
                {
                    if (alteration != ChordTypeAmbiguousAddition.Plus || options.PlusSeventhMeaning != PlusSeventhMeaning.MajorSeventh)
                        return Fail(ChordParseError.UnsupportedAlteration, 7);

                    extensionDegrees.Add(7);
                    majorSeventh = true;
                    logic |= ChordTypeParseLogic.PlusRaisedSeventh;
                    continue;
                }

                var altered = (degree, alteration is ChordTypeAmbiguousAddition.Plus or ChordTypeAmbiguousAddition.Sharp) switch
                {
                    (2, false) => ChordTypeAdditions.Flat2,
                    (4, true) => ChordTypeAdditions.Sharp4,
                    (5, false) => ChordTypeAdditions.Flat5,
                    (5, true) => ChordTypeAdditions.Sharp5,
                    (6, false) => ChordTypeAdditions.Flat6,
                    (9, false) => ChordTypeAdditions.Flat9,
                    (9, true) => ChordTypeAdditions.Sharp9,
                    (11, false) => ChordTypeAdditions.Flat11,
                    (11, true) => ChordTypeAdditions.Sharp11,
                    (13, false) => ChordTypeAdditions.Flat13,
                    (13, true) => ChordTypeAdditions.Sharp13,
                    _ => ChordTypeAdditions.None,
                };

                if (altered == ChordTypeAdditions.None)
                    return Fail(ChordParseError.UnsupportedAlteration, 7);

                additions |= altered;

                if (kind == DegreeKind.Bare && degree == 9)
                    alteredBareNinth = true;

                continue;
            }

            switch (kind, degree)
            {
                case (DegreeKind.Major, _):
                    extensionDegrees.Add(degree);
                    majorSeventh = true;
                    break;

                case (_, 7):
                    extensionDegrees.Add(7);
                    break;

                // "Gadd5": the fifth is there anyway.
                case (DegreeKind.Added, 5):
                    logic |= ChordTypeParseLogic.RedundantFifth;
                    break;

                case (DegreeKind.Added, _):
                    additions |= degree switch
                    {
                        2 => ChordTypeAdditions.Add2,
                        4 => ChordTypeAdditions.Add4,
                        6 => ChordTypeAdditions.Add6,
                        _ => AddedUpperDegree(degree),
                    };
                    break;

                case (DegreeKind.Bare, 9 or 11 or 13):
                    bareUpper.Add((degree, separated));
                    break;

                case (DegreeKind.Bare, 6):
                    additions |= ChordTypeAdditions.Add6;
                    break;

                case (DegreeKind.Bare, 2):
                    bareSecond = true;
                    break;

                case (DegreeKind.Bare, 4):
                    bareFourth = true;
                    break;

                case (DegreeKind.Bare, 5):
                    bareFifth = true;
                    bareFifthSeparated = separated;
                    break;
            }
        }

        // A bare altered ninth is the dominant ninth with the altered ninth: "C9-", "C-9", "C(b9)" are C7(b9); but "C6/9-" has no seventh.
        var impliedSeventh = alteredBareNinth && !additions.HasFlag(ChordTypeAdditions.Add6);
        if (impliedSeventh)
            logic |= ChordTypeParseLogic.AlteredDegreeImpliedSeventh;

        var hasSeventh = extensionDegrees.Any() || impliedSeventh || halfDiminished;
        if (bareUpper.Any())
        {
            if (!hasSeventh && additions.HasFlag(ChordTypeAdditions.Add6))
            {
                // The sixth with the ninth: "C6/9", "C69", "C6(9)".
                logic |= ChordTypeParseLogic.DegreeAddedToSixth;
                foreach (var (degree, _) in bareUpper)
                    additions |= AddedUpperDegree(degree);
            }
            else if (!hasSeventh && bareUpper.All(x => x.separated))
            {
                // The upper degree in parentheses or after a slash, without a seventh: "C(9)" and "C/9" are Cadd9.
                logic |= ChordTypeParseLogic.ParenthesizedDegreeAdded;
                foreach (var (degree, _) in bareUpper)
                    additions |= AddedUpperDegree(degree);
            }
            else if (!hasSeventh && bareUpper.All(x => x.degree == 9) && !qualities.Overlaps([ChordMainType.Minor, ChordMainType.Dim, ChordMainType.Dim7, ChordMainType.Sus2, ChordMainType.Sus4]))
            {
                // A bare ninth without a seventh: "C9" — the dominant ninth or the added ninth, see the options.
                logic |= ChordTypeParseLogic.BareNinth;
                if (options.BareNinthMeaning == BareNinthMeaning.Added)
                    additions |= ChordTypeAdditions.Add9;
                else
                    extensionDegrees.Add(9);
            }
            else
            {
                foreach (var (degree, _) in bareUpper)
                    extensionDegrees.Add(degree);
            }
        }

        if (impliedSeventh && !extensionDegrees.Any())
            extensionDegrees.Add(7);

        #endregion

        #region Qualities: the third and the fifth.

        var minor = qualities.Remove(ChordMainType.Minor);
        var majorWord = qualities.Remove(ChordMainType.Major);
        var dim = qualities.Remove(ChordMainType.Dim);
        var dim7 = qualities.Remove(ChordMainType.Dim7);
        var aug = qualities.Remove(ChordMainType.Aug);
        var sus2 = qualities.Remove(ChordMainType.Sus2);
        var sus4 = qualities.Remove(ChordMainType.Sus4);
        var power = qualities.Remove(ChordMainType.Power);

        var thirdWritten = minor || majorWord || dim || dim7 || aug || halfDiminished;

        if (minor && majorWord)
            return Fail(ChordParseError.IncompatibleQualities, 9);

        if (bareFifth)
        {
            // The minor written as "m" (a leading minus with a fifth is a tab fragment rather: "A-5-5").
            var onlyMinor = minor && !(majorWord || dim || dim7 || aug || halfDiminished) && !logic.HasFlag(ChordTypeParseLogic.MinusAsMinor);
            if (!thirdWritten && (!extensionDegrees.Any() || bareFifthFirst))
            {
                power = true;

                // The power chord with an altered fifth written beside it: "A5#5" is A, E, F; "F5(b5)" is F, C, B.
                if ((additions & (ChordTypeAdditions.Flat5 | ChordTypeAdditions.Sharp5)) != 0)
                {
                    logic |= ChordTypeParseLogic.AlteredFifthAddedToPower;
                    if (additions.HasFlag(ChordTypeAdditions.Flat5))
                        additions = additions & ~ChordTypeAdditions.Flat5 | ChordTypeAdditions.Sharp11;
                    if (additions.HasFlag(ChordTypeAdditions.Sharp5))
                        additions = additions & ~ChordTypeAdditions.Sharp5 | ChordTypeAdditions.Flat13;
                }
            }
            else if (logic.HasFlag(ChordTypeParseLogic.MinusAsMinor))
            {
                return Fail(ChordParseError.UnexpectedFifth, 10);
            }
            else if (onlyMinor && !extensionDegrees.Any())
            {
                // "Em5" is the power chord E5, named from the minor shape.
                logic |= ChordTypeParseLogic.PowerOverMinor;
                minor = false;
                thirdWritten = false;
                power = true;
            }
            else if (onlyMinor && bareFifthSeparated && extensionDegrees.Any() && !majorSeventh)
            {
                // "Bm7/5" is Bm7/5- with the minus lost: the half-diminished chord.
                logic |= ChordTypeParseLogic.FlatFifthAfterMinorSeventh;
                additions |= ChordTypeAdditions.Flat5;
            }
            else
            {
                // "G#75", "Gmaj75", "D9/5": the fifth is there anyway.
                logic |= ChordTypeParseLogic.RedundantFifth;
            }
        }

        if (bareSecond)
        {
            logic |= ChordTypeParseLogic.BareSecond;
            if (!thirdWritten && options.BareSecondMeaning == BareSecondMeaning.Suspended)
                sus2 = true;
            else
                additions |= ChordTypeAdditions.Add2;
        }

        if (bareFourth)
        {
            if (!thirdWritten)
                logic |= ChordTypeParseLogic.BareFourth;

            if (!thirdWritten && options.BareFourthMeaning == BareFourthMeaning.Suspended)
                sus4 = true;
            else
                additions |= ChordTypeAdditions.Add4;
        }

        if (sus2 && sus4)
        {
            sus2 = false;
            additions |= ChordTypeAdditions.Add2;
        }

        var sus = sus2 || sus4;
        if (sus && minor && !(majorWord || dim || dim7 || aug || halfDiminished))
        {
            // The suspension replaces the third, the minor is the shape it was taken from: "Amsus2" is Asus2.
            logic |= ChordTypeParseLogic.SuspensionOverMinor;
            minor = false;
            thirdWritten = false;
        }

        if (sus && thirdWritten)
            return Fail(ChordParseError.IncompatibleQualities, 9);

        if (power && sus)
            power = false;

        if (power && thirdWritten)
            return Fail(ChordParseError.IncompatibleQualities, 9);

        ChordMainType mainType;
        if (halfDiminished)
        {
            if (majorWord || aug || dim7 || dim)
                return Fail(ChordParseError.IncompatibleQualities, 9);

            if (majorSeventh)
                return Fail(ChordParseError.IncompatibleExtension, 11);

            mainType = ChordMainType.Dim;
            extensionDegrees.Add(7);
        }
        else if (dim7)
        {
            if (majorWord || aug || sus || power)
                return Fail(ChordParseError.IncompatibleQualities, 9);

            if (majorSeventh || extensionDegrees.Any(x => x != 7))
                return Fail(ChordParseError.IncompatibleExtension, 11);

            mainType = ChordMainType.Dim7;
            extensionDegrees.Clear();
        }
        else if (dim)
        {
            if (majorWord || aug || sus || power)
                return Fail(ChordParseError.IncompatibleQualities, 9);

            mainType = ChordMainType.Dim;
            if (extensionDegrees.Any() && !majorSeventh)
            {
                // The diminished triad with a plain seventh written apart is the diminished seventh chord: "Cdim(7)", "C7dim".
                logic |= ChordTypeParseLogic.DiminishedWithSeventh;
                mainType = ChordMainType.Dim7;
                foreach (var degree in extensionDegrees.Where(x => x > 7))
                    additions |= AddedUpperDegree(degree);

                extensionDegrees.Clear();
            }
            else if (!extensionDegrees.Any())
            {
                logic |= ChordTypeParseLogic.DimWithoutSeventh;
                if (options.DimWithoutSeventhMeaning == DimWithoutSeventhMeaning.Seventh)
                    mainType = ChordMainType.Dim7;
            }
        }
        else if (aug)
        {
            if (sus || power)
                return Fail(ChordParseError.IncompatibleQualities, 9);

            if (minor)
            {
                mainType = ChordMainType.Minor;
                additions |= ChordTypeAdditions.Sharp5;
            }
            else
            {
                mainType = ChordMainType.Aug;
            }
        }
        else if (minor)
            mainType = ChordMainType.Minor;
        else if (sus4)
            mainType = ChordMainType.Sus4;
        else if (sus2)
            mainType = ChordMainType.Sus2;
        else if (power)
            mainType = ChordMainType.Power;
        else
            mainType = ChordMainType.Major;

        #endregion

        #region The extension: the lowest written degree with its quality; the higher written degrees are added ("C7/13" is C7add13, "C13" includes the ninth).

        ChordTypeExtension? extension = null;
        if (extensionDegrees.Any())
        {
            var lowest = extensionDegrees.Min;
            extension = (lowest, majorSeventh) switch
            {
                (7, false) => ChordTypeExtension.X7,
                (9, false) => ChordTypeExtension.X9,
                (11, false) => ChordTypeExtension.X11,
                (13, false) => ChordTypeExtension.X13,
                (7, true) => ChordTypeExtension.XMaj7,
                (9, true) => ChordTypeExtension.XMaj9,
                (11, true) => ChordTypeExtension.XMaj11,
                _ => ChordTypeExtension.XMaj13,
            };

            foreach (var degree in extensionDegrees.Where(x => x > lowest))
                additions |= AddedUpperDegree(degree);
        }

        #endregion

        return (new ChordType(mainType, extension, additions).Normalize(), rootShift, null, logic, 12, cost);
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
        if (options.ForgiveUnclosedParentheses
            && input.Count(c => c == '(') == input.Count(c => c == ')') + 1
            && input.LastIndexOf('(') > input.LastIndexOf(')'))
        {
            input += ")";
        }

        // The text outside the parentheses is matched atomically: a nested quantifier there backtracks exponentially on unbalanced input.
        var match1 = Regex.Match(input, @"^((?<notin>(?>[^\(\)]+))|\((?<in>[^\(\)]+)\))*$");
        if (!match1.Success)
        {
            return ([(input, false)], null);
        }

        var inputs = match1.Groups["in"].Captures.Select(c => (c, fromParentheses: true))
            .Concat(match1.Groups["notin"].Captures.Select(c => (c, fromParentheses: false)))
            .Where(c => c.c.Length > 0)
            .OrderBy(c => c.c.Index)
            .Select(c => (c.c.Value, c.fromParentheses))
            .SelectMany(c => !c.fromParentheses ? c.Once() : c.Value.Split(',').Select(x => (Value: x, fromParentheses: true)))
            .Where(c => c.Value != string.Empty)
            .ToList();

        var trimmed = inputs.Select(x => x with { Value = x.Value.Trim() }).Where(x => x.Value != string.Empty).ToList();
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

        if (options.ChordTypeParsingOptions.IgnoreTrailingPunctuation)
            Add(ChordTypeMeaninglessAddition.Punctuation);


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