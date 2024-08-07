﻿using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using HarmonyDB.Common;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Index;
using HarmonyDB.Index.Analysis.Models.V1;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Tools;

public static class TonalitiesExtensions
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
        return scale.isMinor ? scale.GetRelativeScale(isSong).root : scale.root;
    }

    public static (byte root, bool isMinor) GetRelativeScale(this (byte root, bool isMinor) scale, bool isSong)
    {
        return scale.isMinor
            ? (Note.Normalize(scale.root + (isSong ? 3 : -3)), false)
            : (Note.Normalize(scale.root + (isSong ? -3 : 3)), true);
    }

    public static byte GetMajorTonic(byte root, bool isMinor, bool isSong)
        => (root, isMinor).GetMajorTonic(isSong);

    public static (byte root, bool isMinor) GetRelativeScale(byte root, bool isMinor, bool isSong)
        => (root, isMinor).GetRelativeScale(isSong);

    public static float TonalityConfidence(this float[] probabilities)
        => probabilities.Max();

    public static float TonicConfidence(this float[] probabilities, bool isSong)
        => Enumerable.Range(0, Note.Modulus)
            .Select(x => probabilities[ToIndex((byte)x, true)] + probabilities[GetRelativeScale((byte)x, true, isSong).ToIndex()])
            .Max();

    public static byte GetAmCTonic(this (byte root, bool isMinor) predicted)
        => predicted.isMinor ? predicted.root : Note.Normalize(predicted.root + 3);

    public static (byte root, bool isMinor) GetAmCRoot(this (byte root, bool isMinor) predicted)
        => (predicted.isMinor ? (byte)0 : (byte)3, predicted.isMinor);

    public static (byte root, ChordType chordType) ToRequiredChordValue(this ChordDataV1 chordDataV1)
        => (chordDataV1.HarmonyData!.Root, chordDataV1.HarmonyData!.ChordType);

    public static string GetTitle(this string normalized, (byte root, bool isMinor)? predicted, bool loopify = false, bool shiftLoop = true)
        => predicted.HasValue
            ? normalized.DeserializeLoop().GetTitle(predicted?.GetAmCRoot(), predicted.Value.GetAmCTonic(), loopify, shiftLoop)
            : normalized.GetTitle(loopify: loopify);

    public static string GetTitle(this string normalized, byte beginningNote = 0, bool loopify = false)
        => normalized.DeserializeLoop().GetTitle(null, beginningNote, loopify, false);

    public static string GetTitle(this string normalized, (byte root, bool isMinor) predicted, byte beginningNote, bool loopify = false)
        => normalized.DeserializeLoop().GetTitle(predicted, beginningNote, loopify);

    public static string GetTitle(this ReadOnlyMemory<CompactHarmonyMovement> sequence, (byte root, bool isMinor)? predicted, byte beginningNote = 0, bool loopify = false, bool shiftLoop = true)
    {
        return string.Join(" ",
                   sequence.ToSequence(predicted, beginningNote, out _, loopify, shiftLoop)
                       .Select(x => x.ToChord()));
    }

    public static string GetFunctionsTitle(this string normalized, (byte root, bool isMinor) predicted, bool loopify = false, bool shiftLoop = true)
        => normalized.DeserializeLoop().GetFunctionsTitle(predicted, loopify, shiftLoop);

    public static string GetFunctionsTitle(this ReadOnlyMemory<CompactHarmonyMovement> sequence, (byte root, bool isMinor) predicted, bool loopify = false, bool shiftLoop = true)
    {
        var beginningNote = predicted.GetAmCTonic();
        predicted = predicted.GetAmCRoot();
        return string.Join(
                   " ",
                   sequence
                       .ToSequence(predicted, beginningNote, out _, loopify, shiftLoop)
                       .Select(x => x.ToFunction(predicted)));
    }

    public static IEnumerable<(byte note, ChordType chordType)> ToSequence(
        this ReadOnlyMemory<CompactHarmonyMovement> sequence,
        (byte root, bool isMinor)? predicted,
        byte beginningNote,
        out int? shift,
        bool loopify = false,
        bool shiftLoop = true)
        => (beginningNote, chordType: sequence.Span[0].FromType)
            .Once()
            .Concat(
                MemoryMarshal.ToEnumerable(sequence)
                    .SkipLast(1)
                    .Select(m =>
                    {
                        beginningNote = Note.Normalize(beginningNote + m.RootDelta);
                        return (beginningNote, chordType: m.ToType);
                    }))
            .ShiftLoop(
                shiftLoop && predicted.HasValue,
                x =>
                    x.beginningNote != predicted!.Value.root
                        ? 0
                        : (predicted.Value.isMinor ? ChordType.Minor : ChordType.Major) == x.chordType
                            ? 2
                            : 1,
                out shift)
            .Loopify(loopify);

    public static string ToChord(this (byte note, ChordType chordType) chord) => ToChord(chord.note, chord.chordType);

    public static string ToChord(byte note, ChordType chordType) => $"{new Note(note).Representation(new())}{chordType.ChordTypeToString()}";

    public static string ToFunction(this (byte note, ChordType chordType) chord, (byte tonic, bool isMinor) tonality)
    {
        byte?[] majorDegrees = [1, null, 2, null, 3, 4, null, 5, null, 6, null, 7];
        byte?[] minorDegrees = [1, null, 2, 3, null, 4, null, 5, 6, null, 7, null];
        var majorRomanDegrees = "\u2160\u2161\u2162\u2163\u2164\u2165\u2166".Select(c => c.ToString()).ToArray();
        var minorRomanDegrees = "\u2170\u2171\u2172\u2173\u2174\u2175\u2176".Select(c => c.ToString()).ToArray();
        var unknownRomanDegrees = "1234567".Select(c => c.ToString()).ToArray();
        bool?[] majorScaleDefaultIsMinorDegrees = [false, true, true, false, false, true, null];
        bool?[] minorScaleDefaultIsMinorDegrees = [true, null, false, true, true, false, false];
        const string signFlat = "♭";

        var shift = Note.Normalize(chord.note - tonality.tonic);
        var degrees = tonality.isMinor ? minorDegrees : majorDegrees;
        var defaultIsMinorDegrees = tonality.isMinor ? minorScaleDefaultIsMinorDegrees : majorScaleDefaultIsMinorDegrees;

        var unstableDegree = degrees[shift];
        var flat = false;
        if (unstableDegree is not { } degree)
        {
            degree = shift == degrees.Length - 1 ? degrees[0]!.Value : degrees[shift + 1]!.Value;
            flat = true;
        }

        var romanDegrees = chord.chordType switch
        {
            ChordType.Major => majorRomanDegrees,
            ChordType.Minor => minorRomanDegrees,
            ChordType.Diminished => minorRomanDegrees,
            ChordType.Augmented => majorRomanDegrees,
            ChordType.Unknown or ChordType.Power or ChordType.Sus2 or ChordType.Sus4 
                => !unstableDegree.HasValue 
                    ? unknownRomanDegrees
                    : defaultIsMinorDegrees[degree - 1] switch
                    {
                        null => unknownRomanDegrees,
                        true => minorRomanDegrees,
                        false => majorRomanDegrees,
                    },
            _ => throw new ArgumentOutOfRangeException()
        };

        return (flat ? signFlat : string.Empty)
               + romanDegrees[degree - 1]
               + chord.chordType.ChordTypeToString(ChordTypePresentation.Degrees);
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

    public static float GetWeight(float occurrences, float successions, int loopLength, bool isSongKnownTonality)
    {
        return (occurrences + successions * 4)
               * (loopLength == 2 ? 1 : 5)
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
                predicted.Value.root 
                - tonality.root
                + (predicted.Value.isMinor, tonality.isMinor) switch
                {
                    (true, true) => 0,
                    (true, false) => 0,
                    (false, true) => 3,
                    (false, false) => 3,
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