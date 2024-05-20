using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.V1;
using HarmonyDB.Index.Analysis.Tools;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Services;

public class ProgressionsBuilder
{
    private readonly ILogger<ProgressionsBuilder> _logger;

    public ProgressionsBuilder(ILogger<ProgressionsBuilder> logger)
    {
        _logger = logger;
    }

    public ChordsProgression BuildProgression(List<ChordProgressionDataV1> sequence, bool buildStandardToo = false)
    {
        var siblingStandards = BuildSiblingStandards(sequence);

        var wants = siblingStandards.Select(x => (
                x.current,
                x.previousStandard,
                x.previousStandardIndex,
                x.nextStandard,
                compatibleWithPrevious: x.current.IsStandard
                    ? x.current.HarmonyData == x.previousStandard?.HarmonyData
                    : x.current.HarmonyData != null && x.previousStandard?.HarmonyData!.Root == x.current.HarmonyData.Root,
                compatibleWithNext: x.current.IsStandard
                    ? x.current.HarmonyData == x.nextStandard?.HarmonyData
                    : x.current.HarmonyData != null && x.nextStandard?.HarmonyData!.Root == x.current.HarmonyData.Root,
                previousAndNextAreCompatible: x is { previousStandard: { }, nextStandard: { } } && x.previousStandard.HarmonyData == x.nextStandard.HarmonyData
            ))
            .ToList();

        var nonStandardsGoTo = wants
            .WithIndices()
            .Where(x => !x.x.current.IsStandard)
            .GroupBy(x => x.x.previousStandardIndex)
            .Select(g => g.ToList())
            .Select(c =>
            {
                var empty = c.Take(0).Select(x => x.i).ToList();

                // all want to go to next
                if (c.All(x => x.x is { compatibleWithPrevious: true, compatibleWithNext: false }))
                    return (
                        c,
                        sameGroup: false,
                        goToPrevious: c.Select(x => x.i).ToList(),
                        incompatible: empty,
                        goToNext: empty);

                // all want to go to previous
                if (c.All(x => x.x is { compatibleWithPrevious: false, compatibleWithNext: true }))
                    return (
                        c,
                        sameGroup: false,
                        goToPrevious: empty,
                        incompatible: empty,
                        goToNext: c.Select(x => x.i).ToList());

                // all want to go neither ways
                if (c.All(x => x.x is { compatibleWithPrevious: false, compatibleWithNext: false }))
                    return (
                        c,
                        sameGroup: false,
                        goToPrevious: empty,
                        incompatible: c.Select(x => x.i).ToList(),
                        goToNext: empty);

                // all want to go both ways
                if (c.All(x => x.x is { compatibleWithPrevious: true, compatibleWithNext: true, previousAndNextAreCompatible: true }))
                    return (
                        c,
                        sameGroup: true,
                        goToPrevious: empty,
                        incompatible: empty,
                        goToNext: empty);

                // all want to go both ways but they are incompatible
                if (c.All(x => x.x is { compatibleWithPrevious: true, compatibleWithNext: true, previousAndNextAreCompatible: false }))
                    return (
                        c,
                        sameGroup: false,
                        goToPrevious: empty,
                        incompatible: c.Select(x => x.i).ToList(),
                        goToNext: empty);

                var goToPrevious = c.TakeWhile(x => x.x.compatibleWithPrevious).Select(x => x.i).ToList();
                var goToNext = c.AsEnumerable().Reverse().TakeWhile(x => x.x.compatibleWithNext).Take(c.Count - goToPrevious.Count).Reverse().Select(x => x.i).ToList();
                return (
                    c,
                    sameGroup: false,
                    goToPrevious,
                    incompatible: c.Skip(goToPrevious.Count).Take(c.Count - goToPrevious.Count - goToNext.Count).Select(x => x.i).ToList(),
                    goToNext);
            })
            .SelectMany(c => c.c.Select(x => (x, c)))
            .ToDictionary(x => x.x.i, x => (
                x.c.sameGroup,
                goToPrevious: x.c.goToPrevious.Contains(x.x.i),
                goToNext: x.c.goToNext.Contains(x.x.i),
                incompatible: x.c.incompatible.Contains(x.x.i)));

        var harmonySequence = new List<HarmonyGroup>();
        var originalSequence = new List<ChordProgressionDataV1>();
        HarmonyDataV1? harmonyData = null;
        int? minChordIndex = null, maxChordIndex = null;

        void Complete()
        {
            if (!minChordIndex.HasValue) return;

            if (!originalSequence.Any())
                throw new("Could not have happened.");

            string? harmonyRepresentation;
            if (harmonyData != null)
            {
                harmonyRepresentation = $"{new Note(harmonyData.Root, harmonyData.Alteration).Representation(new())}{harmonyData.ChordType.ChordTypeToString()}";
            }
            else
            {
                var recognized = originalSequence
                    .Where(x => x.HarmonyData != null)
                    .GroupBy(x => (x.HarmonyData!.Root, x.HarmonyData.Alteration))
                    .SingleOrDefault();

                if (recognized != null)
                {
                    var root = new Note(recognized.Key.Root, recognized.Key.Alteration).Representation(new());
                    var types = recognized.Select(x => x.HarmonyData!.ChordType).Distinct().ToList();
                    var chordType = types.Count == 1 ? types.Single() : ChordType.Unknown;
                    harmonyRepresentation = $"{root}{chordType.ChordTypeToString()}";
                    harmonyData = new()
                    {
                        Root = recognized.Key.Root,
                        Alteration = recognized.Key.Alteration,
                        ChordType = chordType,
                    };
                }
                else
                {
                    harmonyRepresentation = "?";
                }
            }

            harmonySequence.Add(new()
            {
                Index = harmonySequence.Count,
                StartChordIndex = minChordIndex!.Value,
                EndChordIndex = maxChordIndex!.Value,
                OriginalSequence = originalSequence,
                HarmonyData = harmonyData,
                HarmonyRepresentation = harmonyRepresentation,
            });

            originalSequence = new();
            harmonyData = null;
            minChordIndex = maxChordIndex = null;
        }

        foreach (var ((current, _, _, _, _, _, _), i) in wants.WithIndices())
        {
            bool complete;
            if (current.IsStandard)
            {
                complete =
                    // ReSharper disable once MergeConditionalExpression
                    harmonyData == null
                        ? originalSequence.Any() && nonStandardsGoTo[i - 1].SelectSingle(x => x.goToPrevious || x.incompatible)
                        : !harmonyData.Equals(current.HarmonyData);
            }
            else
            {
                var (sameGroup, goToPrevious, goToNext, incompatible) = nonStandardsGoTo[i];
                if (sameGroup || goToPrevious)
                {
                    complete = false;
                }
                else if (incompatible)
                {
                    complete = originalSequence.Any() &&
                               (!nonStandardsGoTo.TryGetValue(i - 1, out var previous)
                                || !previous.incompatible
                                || originalSequence[^1].SelectSingle(x => x.HarmonyData?.Root != current.HarmonyData?.Root || x.HarmonyData?.Alteration != current.HarmonyData?.Alteration));
                }
                else if (goToNext)
                {
                    complete = originalSequence.Any() &&
                               (!nonStandardsGoTo.TryGetValue(i - 1, out var previous) || !previous.goToNext);
                }
                else
                    throw new("Could not have happened, one of the four flags should be set.");
            }

            if (complete)
                Complete();

            originalSequence.Add(current);
            minChordIndex = Math.Min(i, minChordIndex ?? i);
            maxChordIndex = Math.Max(i, maxChordIndex ?? i);
            if (current.IsStandard)
            {
                harmonyData ??= current.HarmonyData;
            }
        }

        Complete();

        var (standardMovementsSequences, extendedMovementsSequences) = GetHarmonyMovementsSequences(harmonySequence, buildStandardToo);

        var (standardGroupsAttribution, standardChordsAttribution) = buildStandardToo 
            ? GetSequenceAttributions(standardMovementsSequences!)
            : (null, null);

        var (extendedGroupsAttribution, extendedChordsAttribution) = GetSequenceAttributions(extendedMovementsSequences);

        return new()
        {
            HarmonySequence = harmonySequence.Select(x => (x, standardGroupsAttribution?.GetValueOrDefault(x), extendedGroupsAttribution.GetValueOrDefault(x))).ToList(),
            OriginalSequence = harmonySequence.SelectMany(x => x.OriginalSequence.WithIndices().Select(y => (
                y.x, x,
                y.i,
                (standardChordsAttribution?.GetValueOrDefault(x.StartChordIndex + y.i)),
                extendedChordsAttribution.GetValueOrDefault(x.StartChordIndex + y.i)))).ToList(),
            StandardHarmonyMovementsSequences = standardMovementsSequences,
            ExtendedHarmonyMovementsSequences = extendedMovementsSequences,
        };
    }

    private static (Dictionary<HarmonyGroup, HarmonyMovementsSequence> groupsAttribution, Dictionary<int, HarmonyMovementsSequence> chordsAttribution) GetSequenceAttributions(IReadOnlyList<HarmonyMovementsSequence> movementsSequences)
    {
        var sequencesAttribution = movementsSequences
            .SelectMany(s => s.Movements.WithIsLast().SelectMany(m => m.x.From.Once().SelectSingle(x => m.isLast ? x.Append(m.x.To) : x).Select(g => (g, s))))
            .ToDictionary(x => x.g, x => x.s);

        var chordsAttribution = sequencesAttribution
            .SelectMany(s =>
                s.Key.OriginalSequence.WithIndices().Select(c => (chordIndex: c.i + s.Key.StartChordIndex, s)))
            .ToDictionary(x => x.chordIndex, x => x.s.Value);

        return (sequencesAttribution, chordsAttribution);
    }

    private static (IReadOnlyList<HarmonyMovementsSequence>? standardMovementsSequences, IReadOnlyList<HarmonyMovementsSequence> extendedMovementsSequences) GetHarmonyMovementsSequences(List<HarmonyGroup> sequence, bool buildStandardToo)
        => (buildStandardToo ? GetHarmonyMovementsSequences(sequence, IsValidStandard) : null,
            GetHarmonyMovementsSequences(sequence, IsValidExtended));

    internal static bool IsValidExtended(HarmonyGroup g)
    {
        return g.HarmonyData != null;
    }

    private static bool IsValidStandard(HarmonyGroup g)
    {
        return g.IsStandard;
    }

    internal static IReadOnlyList<HarmonyMovementsSequence> GetHarmonyMovementsSequences(List<HarmonyGroup> sequence, Func<HarmonyGroup, bool> isValid) =>
        sequence.WithPrevious()
            .Where(x => x.previous != null)
            .ToChunks(pair => isValid(pair.previous!) && isValid(pair.current))
            .Where(c => c.criterium)
            .Select(c => new HarmonyMovementsSequence
            {
                FirstRoot = c.chunk.First().previous!.HarmonyData!.Root,
                Movements = c.chunk.Select(p =>
                {
                    var delta = Note.Normalize(p.current.HarmonyData!.Root - p.previous.HarmonyData!.Root);
                    var titleDelta = delta > 7 ? delta - Note.Modulus : delta;

                    return new HarmonyMovement
                    {
                        RootDelta = delta,
                        From = p.previous,
                        To = p.current,
                        FromType = p.previous.HarmonyData.ChordType,
                        ToType = p.current.HarmonyData.ChordType,
                        Title = $"{(titleDelta > 0 ? "+" : null)}{titleDelta} {p.previous.HarmonyData.ChordType.ChordTypeToString(true)}-{p.current.HarmonyData.ChordType.ChordTypeToString(true)}",
                    };
                }).ToList(),
            })
            .ToList();

    private static List<(ChordProgressionDataV1 current, ChordProgressionDataV1? previousStandard, int? previousStandardIndex, ChordProgressionDataV1? nextStandard)> BuildSiblingStandards(List<ChordProgressionDataV1> sequence)
    {
        var previousStandard = (ChordProgressionDataV1?)null;
        int? previousStandardIndex = null;
        var currentPromise = new Promise<ChordProgressionDataV1>();
        var withPreviousAndNextPromise = new List<(ChordProgressionDataV1 current, ChordProgressionDataV1? previousStandard, int? previousStandardIndex, Promise<ChordProgressionDataV1>? nextStandard)>();

        foreach (var (current, i) in sequence.WithIndices())
        {
            if (current.IsStandard)
            {
                currentPromise.Value = current;
                currentPromise = new();
                withPreviousAndNextPromise.Add((current, previousStandard, previousStandardIndex, currentPromise));
                previousStandard = current;
                previousStandardIndex = i;
            }
            else
            {
                withPreviousAndNextPromise.Add((current, previousStandard, previousStandardIndex, currentPromise));
            }
        }

        return withPreviousAndNextPromise
            .Select(x => (x.current, x.previousStandard, x.previousStandardIndex, x.nextStandard?.Value))
            .ToList();
    }

    private class Promise<T>
    {
        public T? Value { get; set; }
    }
}