using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using HarmonyDB.Common;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Index;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Tools;

public static class TonalitiesAndStructuresExtensions
{
    private static readonly ConcurrentDictionary<string, (byte root, bool isMinor)?> Cache = new();

    public const int ProbabilitiesLength = Note.Modulus * 2;

    public static float[] ToLinear(this float[,] probabilities)
    {
        var linearArray = new float[ProbabilitiesLength];

        for (byte root = 0; root < Note.Modulus; root++)
        {
            for (var i = 0; i < 2; i++)
            {
                var isMinor = i == 1;
                var index = ToIndex(root, isMinor);
                linearArray[index] = probabilities[root, isMinor ? 1 : 0];
            }
        }

        return linearArray;
    }

    public static int ToIndex(byte root, bool isMinor) => root * 2 + (isMinor ? 1 : 0);

    public static int ToIndex(this (byte root, bool isMinor) index) => ToIndex(index.root, index.isMinor);

    public static (byte root, bool isMinor) FromIndex(this int index) => ((byte)(index / 2), index % 2 == 1);

    public static (byte root, bool isMinor) GetPredictedTonality(this float[] probabilities)
        => probabilities.GetPredictedTonality(out _);

    public static (byte root, bool isMinor) GetPredictedTonality(this float[] probabilities, out float confidence)
    {
        var predictedTonality = probabilities
            .Select((probability, index) => (probability, index))
            .OrderByDescending(x => x.probability)
            .Select(x => (x, tonality: FromIndex(x.index)))
            .First();

        confidence = predictedTonality.x.probability;
        return predictedTonality.tonality;
    }

    public static (byte root, bool isMinor) GetSecondPredictedTonality(this float[] probabilities)
        => probabilities.GetSecondPredictedTonality(out _);

    public static (byte root, bool isMinor) GetSecondPredictedTonality(this float[] probabilities, out float confidence)
    {
        var result = probabilities
            .Select((probability, index) => (probability, index))
            .OrderByDescending(x => x.probability)
            .Skip(1)
            .Select(x => (x.probability, tonality: FromIndex(x.index)))
            .First();

        confidence = result.probability;
        return result.tonality;
    }

    public static byte GetMajorTonic(this (byte root, bool isMinor) scale, bool isSong)
    {
        return scale.isMinor ? scale.GetParallelScale(isSong).root : scale.root;
    }

    public static (byte root, bool isMinor) GetParallelScale(this (byte root, bool isMinor) scale, bool isSong)
    {
        return scale.isMinor
            ? (Note.Normalize(scale.root + (isSong ? 3 : -3)), false)
            : (Note.Normalize(scale.root + (isSong ? -3 : 3)), true);
    }
    public static byte GetMajorTonic(byte root, bool isMinor, bool isSong)
        => (root, isMinor).GetMajorTonic(isSong);

    public static (byte root, bool isMinor) GetParallelScale(byte root, bool isMinor, bool isSong)
        => (root, isMinor).GetParallelScale(isSong);

    public static float TonalityConfidence(this float[] probabilities)
        => probabilities.Max();

    public static float TonicConfidence(this float[] probabilities, bool isSong)
        => Enumerable.Range(0, Note.Modulus)
            .Select(x => probabilities[ToIndex((byte)x, true)] + probabilities[GetParallelScale((byte)x, true, isSong).ToIndex()])
            .Max();

    public static string GetTitle(this string normalized, byte beginningNote = 0, bool loopify = true) => Loop.Deserialize(normalized).GetTitle(beginningNote, loopify);

    public static string GetTitle(this string normalized, (byte root, bool isMinor)? predicted)
        => predicted.HasValue
            ? normalized.GetTitle(
                predicted.Value.isMinor ? predicted.Value.root : Note.Normalize(predicted.Value.root + 3), false)
            : normalized.GetTitle(loopify: false);

    public static string GetTitle(this ReadOnlyMemory<CompactHarmonyMovement> sequence, byte beginningNote = 0, bool loopify = true)
    {
        return string.Join(" ", ToChord(beginningNote, sequence.Span[0].FromType)
            .Once()
            .Concat(
                MemoryMarshal.ToEnumerable(sequence)
                    .SelectSingle(x => loopify ? x : x.SkipLast(1))
                    .Select(m =>
                    {
                        beginningNote = Note.Normalize(beginningNote + m.RootDelta);
                        return ToChord(beginningNote, m.ToType);
                    })));
    }

    public static string ToChord(this (byte note, ChordType chordType) chord) => ToChord(chord.note, chord.chordType);

    public static string ToChord(byte note, ChordType chordType) => $"{new Note(note).Representation(new())}{chordType.ChordTypeToString()}";

    public static (byte root, bool isMinor)? TryParseBestTonality(this string tonality)
    {
        if (Cache.TryGetValue(tonality, out var cachedResult))
        {
            return cachedResult;
        }

        if (!Note.CharactersToNotes.TryGetValue(tonality[0], out var note))
        {
            Cache[tonality] = null;
            return null;
        }

        tonality = tonality.Substring(1);
        bool isMinor;

        switch (tonality)
        {
            case "#":
                note = note.Sharp();
                isMinor = false;
                break;
            case "b":
                note = note.Flat();
                isMinor = false;
                break;
            case "#m":
                note = note.Sharp();
                isMinor = true;
                break;
            case "bm":
                note = note.Flat();
                isMinor = true;
                break;
            case "":
                isMinor = false;
                break;
            case "m":
                isMinor = true;
                break;
            default:
                Cache[tonality] = null;
                return null;
        }

        var result = (note.Value, isMinor);
        Cache[tonality] = result;
        return result;
    }

    public static float GetWeight(this StructureLink structureLink, StructureLoop loop, bool isSongKnownTonality)
    {
        return (structureLink.Occurrences + structureLink.Successions * 4)
               * (loop.Length == 2 ? 1 : 5)
               * (isSongKnownTonality ? 10 : 1);
    }

    public static (byte root, bool isMinor) FromEm(this (byte tonic, Scale scale) tonality)
        => (tonality.tonic, tonality.scale == Scale.Minor);

    public static string ToSongTonalityTitle(this int tonalityIndex)
        => tonalityIndex
            .FromIndex()
            .ToSongTonalityTitle();

    public static string ToSongTonalityTitle(this (byte root, bool isMinor) tonality)
        => $"{new Note(tonality.root).Representation(new())}{(tonality.isMinor ? "m" : string.Empty)}";

    public static string ToLoopTonalityTitle(this (byte root, bool isMinor) tonality, byte? normalizationRoot)
        => normalizationRoot.HasValue
            ? ToSongTonalityTitle((Note.Normalize(normalizationRoot.Value - tonality.root), tonality.isMinor))
            : tonality.ToLoopTonalityTitle();

    public static string ToLoopTonalityTitle(this int tonalityIndex, (byte root, bool isMinor)? predicted = null)
        => tonalityIndex.FromIndex().ToLoopTonalityTitle(predicted);

    public static string ToLoopTonalityTitle(this (byte root, bool isMinor) tonality,
        (byte root, bool isMinor)? predicted)
        => !predicted.HasValue
            ? tonality.ToLoopTonalityTitle()
            : ToSongTonalityTitle((Note.Normalize(
                tonality.root
                - predicted.Value.root
                - (predicted.Value.isMinor, tonality.isMinor) switch
                {
                    (true, true) => 0,
                    (true, false) => 6,
                    (false, true) => 3,
                    (false, false) => -3,
                }), tonality.isMinor));

    public static string ToLoopTonalityTitle(this (byte root, bool isMinor) tonality)
        => $"{(tonality.isMinor ? "m" : "M")}{tonality.root.ToLoopTonalityShiftTitle()}";

    public static string ToLoopTonalityShiftTitle(this byte root) 
        => root >= LoopTonalityShiftsFirstNegativeRoot ? $"–{-(root - 12)}" : $"+{root}";

    public const byte LoopTonalityShiftsFirstNegativeRoot = 8;

    public static IReadOnlyList<byte> GetLoopTonalityShiftsDisplayOrder((byte root, bool isMinor)? predicted)
        => Enumerable
            .Range(
                predicted.HasValue
                    ? predicted.Value.root - (predicted.Value.isMinor ? 3 : 0)
                    : LoopTonalityShiftsFirstNegativeRoot, Note.Modulus)
            .Select(Note.Normalize)
            .ToList();
}