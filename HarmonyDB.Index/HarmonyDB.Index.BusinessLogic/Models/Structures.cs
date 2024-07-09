using System.Runtime.InteropServices;
using HarmonyDB.Common;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.Index;
using HarmonyDB.Index.Analysis.Tools;
using OneShelf.Common;

namespace HarmonyDB.Index.BusinessLogic.Models;

public class Structures
{
    private Structures(List<StructureLink> links)
    {
        Links = links;

        string ToChord(byte note, ChordType chordType) => $"{new Note(note, NoteAlteration.Sharp).Representation(new())}{chordType.ChordTypeToString()}";

        Loops = links.GroupBy(x => x.Normalized).Select(g =>
        {
            var sequence = Analysis.Models.Loop.Deserialize(g.Key);
            var note = (byte)0;
            return new StructureLoop(
                g.Key,
                sequence.Length,
                g.Sum(x => x.Occurrences),
                g.Sum(x => x.Successions),
                g.Count(),
                string.Join(" ", ToChord(note, sequence.Span[0].FromType)
                    .Once()
                    .Concat(
                        MemoryMarshal.ToEnumerable(sequence)
                            .Select(m =>
                            {
                                note = Note.Normalize(note + m.RootDelta);
                                return ToChord(note, m.ToType);
                            }))));
        }).ToDictionary(x => x.Normalized);
    }

    public IReadOnlyList<StructureLink> Links { get; }

    public IReadOnlyDictionary<string, StructureLoop> Loops { get; }

    public static byte[] Serialize(IReadOnlyList<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions)> compactLinks)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        var normalized = compactLinks.Select(x => x.normalized).Distinct().WithIndices().ToDictionary(x => x.x, x => x.i);
        var externalIds = compactLinks.Select(x => x.externalId).Distinct().WithIndices().ToDictionary(x => x.x, x => x.i);

        binaryWriter.Write(normalized.Count);
        foreach (var pair in normalized.OrderBy(x => x.Value))
        {
            binaryWriter.Write(pair.Key);
        }

        binaryWriter.Write(externalIds.Count);
        foreach (var pair in externalIds.OrderBy(x => x.Value))
        {
            binaryWriter.Write(pair.Key);
        }
        
        binaryWriter.Write(compactLinks.Count);
        foreach (var link in compactLinks)
        {
            binaryWriter.Write(normalized[link.normalized]);
            binaryWriter.Write(externalIds[link.externalId]);
            binaryWriter.Write(link.normalizationRoot);
            binaryWriter.Write(link.occurrences);
            binaryWriter.Write(link.successions);
        }

        return memoryStream.ToArray();
    }

    public static Structures Deserialize(byte[] data)
    {
        using var memoryStream = new MemoryStream(data);
        using var binaryReader = new BinaryReader(memoryStream);

        var normalizedCount = binaryReader.ReadInt32();
        var normalized = new List<string>(normalizedCount);
        for (var i = 0; i < normalizedCount; i++)
        {
            normalized.Add(binaryReader.ReadString());
        }

        var externalIdsCount = binaryReader.ReadInt32();
        var externalIds = new List<string>(externalIdsCount);
        for (var i = 0; i < externalIdsCount; i++)
        {
            externalIds.Add(binaryReader.ReadString());
        }

        var linksCount = binaryReader.ReadInt32();
        var links = new List<StructureLink>(linksCount);
        for (var i = 0; i < linksCount; i++)
        {
            var normalizedIndex = binaryReader.ReadInt32();
            var externalIdIndex = binaryReader.ReadInt32();
            var normalizationRoot = binaryReader.ReadByte();
            var occurrences = binaryReader.ReadInt16();
            var successions = binaryReader.ReadInt16();

            var link = new StructureLink(
                normalized[normalizedIndex],
                externalIds[externalIdIndex],
                normalizationRoot,
                occurrences,
                successions);

            links.Add(link);
        }

        return new(links);
    }
}