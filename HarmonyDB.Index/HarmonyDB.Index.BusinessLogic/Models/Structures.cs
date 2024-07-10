using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.Index;
using OneShelf.Common;

namespace HarmonyDB.Index.BusinessLogic.Models;

public class Structures
{
    private Structures(IReadOnlyList<StructureLink> links, IReadOnlyDictionary<string, StructureLoop> loops, IReadOnlyDictionary<string, StructureSong> songs)
    {
        Links = links;
        Loops = loops;
        Songs = songs;
    }

    private Structures(List<StructureLink> links)
        : this(
            links,
            links.GroupBy(x => x.Normalized).Select(g =>
            {
                var sequence = Loop.Deserialize(g.Key);
                var note = (byte)0;
                return new StructureLoop(
                    g.Key,
                    sequence.Length,
                    g.Sum(x => x.Occurrences),
                    g.Sum(x => x.Successions),
                    g.Select(x => x.ExternalId).Distinct().Count());
            }).ToDictionary(x => x.Normalized),
            links
                .GroupBy(x => x.ExternalId)
                .Select(x => new StructureSong(x.Key, x.Select(x => x.Normalized).Distinct().Count()))
                .ToDictionary(x => x.ExternalId))
    {
    }

    public IReadOnlyList<StructureLink> Links { get; }

    public IReadOnlyDictionary<string, StructureLoop> Loops { get; }

    public IReadOnlyDictionary<string, StructureSong> Songs { get; }

    public Structures Reduce(Func<StructureLink, bool> takeOnly)
    {
        var links = Links.Where(takeOnly).ToList();
        var loopIds = links.Select(x => x.Normalized).ToHashSet();
        var songIds = links.Select(x => x.ExternalId).ToHashSet();
        return new(
            links,
            Loops.Where(p => loopIds.Contains(p.Key)).ToDictionary(x => x.Key, x => x.Value),
            Songs.Where(p => songIds.Contains(p.Key)).ToDictionary(x => x.Key, x => x.Value));
    }

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