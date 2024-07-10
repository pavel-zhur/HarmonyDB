using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using HarmonyDB.Common;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
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
    {
        return probabilities
            .Select((probability, index) => (probability, index))
            .OrderByDescending(x => x.probability)
            .Select(x => FromIndex(x.index))
            .First();
    }

    public static (byte root, bool isMinor) GetSecondPredictedTonality(this float[] probabilities)
    {
        return probabilities
            .Select((probability, index) => (probability, index))
            .OrderByDescending(x => x.probability)
            .Skip(1)
            .Select(x => FromIndex(x.index))
            .First();
    }

    public static byte GetMajorTonic(this (byte root, bool isMinor) scale)
    {
        return scale.isMinor ? scale.GetParallelScale().root : scale.root;
    }

    public static (byte root, bool isMinor) GetParallelScale(this (byte root, bool isMinor) scale)
    {
        return scale.isMinor
            ? (Note.Normalize(scale.root + 3), false)
            : (Note.Normalize(scale.root - 3), true);
    }
    public static byte GetMajorTonic(byte root, bool isMinor)
        => (root, isMinor).GetMajorTonic();

    public static (byte root, bool isMinor) GetParallelScale(byte root, bool isMinor)
        => (root, isMinor).GetParallelScale();

    public static float TonalityConfidence(this float[] probabilities)
        => probabilities.Max();

    public static float TonicConfidence(this float[] probabilities)
        => Enumerable.Range(0, Note.Modulus)
            .Select(x => probabilities[ToIndex((byte)x, true)] + probabilities[GetParallelScale((byte)x, true).ToIndex()])
            .Max();

    public static string GetTitle(this string normalizedLoop)
    {
        string ToChord(byte note, ChordType chordType) => $"{new Note(note, NoteAlteration.Sharp).Representation(new())}{chordType.ChordTypeToString()}";

        var sequence = Loop.Deserialize(normalizedLoop);
        byte note = 0;
        return string.Join(" ", ToChord(note, sequence.Span[0].FromType)
            .Once()
            .Concat(
                MemoryMarshal.ToEnumerable(sequence)
                    .Select(m =>
                    {
                        note = Note.Normalize(note + m.RootDelta);
                        return ToChord(note, m.ToType);
                    })));
    }

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

    public static float Weight(this StructureLink structureLink, StructureLoop loop, bool isSongKnownTonality)
    {
        return (structureLink.Occurrences + structureLink.Successions * 4) * (loop.Length == 2 ? 1 : 5) * (isSongKnownTonality ? 5 : 1);
    }
}