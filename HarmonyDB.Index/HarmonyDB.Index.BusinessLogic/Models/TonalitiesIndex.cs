using System.Runtime.InteropServices;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Common;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Analysis.Tools;
using OneShelf.Common;

namespace HarmonyDB.Index.BusinessLogic.Models;

public class TonalitiesIndex
{
    private TonalitiesIndex(
        List<TonalitiesIndexSong> songsKeys, 
        List<TonalitiesIndexLoop> loopsKeys)
    {
        SongsKeys = songsKeys.ToDictionary(x => x.ExternalId);
        LoopsKeys = loopsKeys.ToDictionary(x => x.Normalized);
    }

    public IReadOnlyDictionary<string, TonalitiesIndexLoop> LoopsKeys { get; }

    public IReadOnlyDictionary<string, TonalitiesIndexSong> SongsKeys { get; }

    public static TonalitiesIndex Deserialize(byte[] serialized)
    {
        using var stream = new MemoryStream(serialized);
        using var reader = new BinaryReader(stream);

        var loopsKeysPartial = Enumerable
            .Range(0, reader.ReadInt32())
            .Select(i => new
            {
                Normalized = reader.ReadString(),
                Probabilities = Enumerable
                    .Range(0, TonalitiesBalancer.ProbabilitiesLength)
                    .Select(_ => reader.ReadSingle())
                    .ToArray(),
            })
            .ToList();

        var songsKeysPartial = Enumerable
            .Range(0, reader.ReadInt32())
            .Select(i => new
            {
                ExternalId = reader.ReadString(),
                Probabilities = Enumerable
                    .Range(0, TonalitiesBalancer.ProbabilitiesLength)
                    .Select(_ => reader.ReadSingle())
                    .ToArray(),
                DespiteStable = reader.ReadBoolean(),
            })
            .ToList();

        var count = reader.ReadInt32();
        var all = new List<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions)>();
        for (var i = 0; i < count; i++)
        {
            all.Add((
                loopsKeysPartial[reader.ReadInt32()].Normalized,
                songsKeysPartial[reader.ReadInt32()].ExternalId,
                reader.ReadByte(),
                reader.ReadInt16(),
                reader.ReadInt16()));
        }

        var allBySongByLoop = all
            .GroupBy(x => x.externalId)
            .ToDictionary(
                x => x.Key,
                x => x
                    .GroupBy(x => x.normalized)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Select(x => new SongLoopLink
                        {
                            NormalizationRoot = x.normalizationRoot,
                            Occurrences = x.occurrences,
                            Successions = x.successions,
                        }).ToList()));

        var allByLoopBySong = all
            .GroupBy(x => x.normalized)
            .ToDictionary(
                x => x.Key,
                x => x
                    .GroupBy(x => x.externalId)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Select(x => new SongLoopLink
                        {
                            NormalizationRoot = x.normalizationRoot,
                            Occurrences = x.occurrences,
                            Successions = x.successions,
                        }).ToList()));

        string ToChord(byte note, ChordType chordType) => $"{new Note(note, NoteAlteration.Sharp).Representation(new())}{chordType.ChordTypeToString()}";

        return new(
            songsKeysPartial
                .Select(x => new TonalitiesIndexSong
                {
                    DespiteStable = x.DespiteStable,
                    ExternalId = x.ExternalId,
                    Probabilities = x.Probabilities,
                    Loops = allBySongByLoop[x.ExternalId],
                })
                .ToList(),
            loopsKeysPartial
                .Select(x =>
                {
                    byte note = 0;
                    var sequence = Loop.Deserialize(x.Normalized);
                    return new TonalitiesIndexLoop
                    {
                        Normalized = x.Normalized,
                        Probabilities = x.Probabilities,
                        Songs = allByLoopBySong[x.Normalized],
                        TotalOccurrences = allByLoopBySong[x.Normalized].Sum(x => x.Value.Sum(x => x.Occurrences)),
                        TotalSuccessions = allByLoopBySong[x.Normalized].Sum(x => x.Value.Sum(x => x.Successions)),
                        Progression = string.Join(" ", ToChord(note, sequence.Span[0].FromType)
                            .Once()
                            .Concat(
                                MemoryMarshal.ToEnumerable(sequence)
                                    //.Take(sequence.Length - 1)
                                    .Select(m =>
                                    {
                                        note = Note.Normalize(note + m.RootDelta);
                                        return ToChord(note, m.ToType);
                                    })))
                    };
                })
                .ToList());
    }

    public static byte[] Serialize(
        (Dictionary<string, (IReadOnlyList<float> probabilities, bool despiteStable)> songsKeys,
            Dictionary<string, float[]> loopsKeys) result,
        List<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions)> all)
    {
        var (songsKeys, loopsKeys) = result;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        var externalIds = new Dictionary<string, int>();
        var normalizedValues = new Dictionary<string, int>();

        writer.Write(loopsKeys.Count);
        foreach (var (key, value) in loopsKeys)
        {
            normalizedValues[key] = normalizedValues.Count;
            writer.Write(key);
            foreach (var f in value)
            {
                writer.Write(f);
            }
        }

        writer.Write(songsKeys.Count);
        foreach (var (key, (probabilities, despiteStable)) in songsKeys)
        {
            externalIds[key] = externalIds.Count;
            writer.Write(key);
            foreach (var f in probabilities)
            {
                writer.Write(f);
            }

            writer.Write(despiteStable);
        }

        writer.Write(all.Count);
        foreach (var (normalized, externalId, normalizationRoot, occurrences, successions) in all)
        {
            writer.Write(normalizedValues[normalized]);
            writer.Write(externalIds[externalId]);
            writer.Write(normalizationRoot);
            writer.Write(occurrences);
            writer.Write(successions);
        }

        writer.Flush();
        return stream.ToArray();
    }
}