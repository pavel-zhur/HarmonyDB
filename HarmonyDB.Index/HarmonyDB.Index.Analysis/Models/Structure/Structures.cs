using System.Buffers.Binary;
using System.Text;
using HarmonyDB.Index.Analysis.Models.Ids;
using HarmonyDB.Index.Analysis.Tools;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Models.Structure;

/// <summary>
/// The loops found in the songs: the links between them in one array, in the order they were serialized,
/// the statistics of the loops and of the songs, and the links of each loop and of each song.
/// </summary>
public class Structures
{
    private const int LinkLength = 21;

    private readonly IndexIds _ids;
    private readonly CompactStructureLink[] _links;
    private readonly StructureLoop[] _loops;
    private readonly int[] _songsTotalLoops;
    private readonly int[] _linksByLoopStarts;
    private readonly int[] _linksByLoop;
    private readonly int[] _linksBySongStarts;
    private readonly int[] _linksBySong;

    private Structures(IndexIds ids, CompactStructureLink[] links, IdPositions loops, IdPositions songs, StructureLoop[] loopsStatistics, int[] songsTotalLoops, int[] linksByLoopStarts, int[] linksByLoop, int[] linksBySongStarts, int[] linksBySong)
    {
        _ids = ids;
        _links = links;
        Loops = loops;
        Songs = songs;
        _loops = loopsStatistics;
        _songsTotalLoops = songsTotalLoops;
        _linksByLoopStarts = linksByLoopStarts;
        _linksByLoop = linksByLoop;
        _linksBySongStarts = linksBySongStarts;
        _linksBySong = linksBySong;
    }

    public ReadOnlySpan<CompactStructureLink> Links => _links;

    /// <summary>
    /// The loops in the order of their first links.
    /// </summary>
    public IdPositions Loops { get; }

    /// <summary>
    /// The songs in the order of their first links.
    /// </summary>
    public IdPositions Songs { get; }

    public bool TryGetLoop(int loop, out StructureLoop statistics)
    {
        statistics = Loops.TryGetPosition(loop, out var position) ? _loops[position] : null!;
        return statistics != null;
    }

    /// <exception cref="KeyNotFoundException">The loop has no links.</exception>
    public StructureLoop GetLoop(int loop) => _loops[Loops.GetPosition(loop)];

    public bool TryGetSong(int song, out StructureSong statistics)
    {
        statistics = Songs.TryGetPosition(song, out var position) ? new(_ids.Songs[song], _songsTotalLoops[position]) : null!;
        return statistics != null;
    }

    /// <exception cref="KeyNotFoundException">The song has no links.</exception>
    public StructureSong GetSong(int song) => new(_ids.Songs[song], _songsTotalLoops[Songs.GetPosition(song)]);

    /// <returns>The loop's links in their order, none if the loop has no links.</returns>
    public IEnumerable<CompactStructureLink> GetLinksByLoop(int loop)
        => Loops.TryGetPosition(loop, out var position) ? GetLinks(_linksByLoop, _linksByLoopStarts, position) : [];

    /// <returns>The song's links in their order, none if the song has no links.</returns>
    public IEnumerable<CompactStructureLink> GetLinksBySong(int song)
        => Songs.TryGetPosition(song, out var position) ? GetLinks(_linksBySong, _linksBySongStarts, position) : [];

    public StructureLink ToStructureLink(CompactStructureLink link)
        => new(_ids.Loops[link.Loop], _ids.Songs[link.Song], link.NormalizationRoot, link.Occurrences, link.Successions, link.Coverage);

    private IEnumerable<CompactStructureLink> GetLinks(int[] links, int[] starts, int position)
    {
        for (var i = starts[position]; i < starts[position + 1]; i++)
        {
            yield return _links[links[i]];
        }
    }

    public static byte[] Serialize(IReadOnlyList<StructureLink> compactLinks)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        var normalized = compactLinks.Select(x => x.Normalized).Distinct().WithIndices().ToDictionary(x => x.x, x => x.i);
        var externalIds = compactLinks.Select(x => x.ExternalId).Distinct().WithIndices().ToDictionary(x => x.x, x => x.i);

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
            binaryWriter.Write(normalized[link.Normalized]);
            binaryWriter.Write(externalIds[link.ExternalId]);
            binaryWriter.Write(link.NormalizationRoot);
            binaryWriter.Write(link.Occurrences);
            binaryWriter.Write(link.Successions);
            binaryWriter.Write(link.Coverage);
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Reads what <see cref="Serialize"/> wrote, straight into the final arrays.
    /// The statistics are those of all the links of each loop and of each song: sums and averages in the links' order,
    /// as LINQ's Sum and Average of floats compute them (in doubles).
    /// </summary>
    public static Structures Read(Stream stream, IndexIds ids)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        var loopsByIndex = ReadIds(reader, ids.Loops);
        var songsByIndex = ReadIds(reader, ids.Songs);

        var links = new CompactStructureLink[reader.ReadInt32()];
        var loops = new FirstAppearances(loopsByIndex);
        var songs = new FirstAppearances(songsByIndex);
        var loopsLinksCounts = new int[loopsByIndex.Length];
        var songsLinksCounts = new int[songsByIndex.Length];
        var totalOccurrences = new double[loopsByIndex.Length];
        var totalSuccessions = new double[loopsByIndex.Length];
        var totalCoverage = new double[loopsByIndex.Length];
        Span<byte> data = stackalloc byte[LinkLength];
        for (var i = 0; i < links.Length; i++)
        {
            // a link as Serialize writes it: the loop's index, the song's index, the root, three floats
            stream.ReadExactly(data);
            var loop = loopsByIndex[BinaryPrimitives.ReadInt32LittleEndian(data)];
            var song = songsByIndex[BinaryPrimitives.ReadInt32LittleEndian(data[4..])];
            var link = links[i] = new(
                loop,
                song,
                data[8],
                BinaryPrimitives.ReadSingleLittleEndian(data[9..]),
                BinaryPrimitives.ReadSingleLittleEndian(data[13..]),
                BinaryPrimitives.ReadSingleLittleEndian(data[17..]));

            var loopPosition = loops.GetPosition(loop);
            loopsLinksCounts[loopPosition]++;
            totalOccurrences[loopPosition] += link.Occurrences;
            totalSuccessions[loopPosition] += link.Successions;
            totalCoverage[loopPosition] += link.Coverage;

            songsLinksCounts[songs.GetPosition(song)]++;
        }

        var loopsPositions = loops.ToIdPositions();
        var songsPositions = songs.ToIdPositions();

        var (linksByLoopStarts, linksByLoop) = GroupLinks(links, loopsLinksCounts, loopsPositions.Count, x => loopsPositions.GetPosition(x.Loop));
        var (linksBySongStarts, linksBySong) = GroupLinks(links, songsLinksCounts, songsPositions.Count, x => songsPositions.GetPosition(x.Song));

        var loopsTotalSongs = CountDistinct(links, linksByLoopStarts, linksByLoop, songsPositions.Count, x => songsPositions.GetPosition(x.Song));
        var songsTotalLoops = CountDistinct(links, linksBySongStarts, linksBySong, loopsPositions.Count, x => loopsPositions.GetPosition(x.Loop));

        var loopsStatistics = new StructureLoop[loopsPositions.Count];
        for (var position = 0; position < loopsStatistics.Length; position++)
        {
            var normalized = ids.Loops[loopsPositions.GetNumber(position)];
            loopsStatistics[position] = new(
                normalized,
                normalized.DeserializeLoop().Length,
                (float)totalOccurrences[position],
                (float)totalSuccessions[position],
                (float)(totalCoverage[position] / loopsLinksCounts[position]),
                loopsTotalSongs[position]);
        }

        return new(ids, links, loopsPositions, songsPositions, loopsStatistics, songsTotalLoops, linksByLoopStarts, linksByLoop, linksBySongStarts, linksBySong);
    }

    private static int[] ReadIds(BinaryReader reader, IdTable table)
    {
        var numbers = new int[reader.ReadInt32()];
        for (var i = 0; i < numbers.Length; i++)
        {
            numbers[i] = table.GetOrAdd(reader.ReadString());
        }

        return numbers;
    }

    // The links' indices grouped by their loops or songs, each group in the links' order.
    private static (int[] starts, int[] links) GroupLinks(CompactStructureLink[] links, int[] counts, int groupsCount, Func<CompactStructureLink, int> getGroup)
    {
        var starts = new int[groupsCount + 1];
        for (var group = 0; group < groupsCount; group++)
        {
            starts[group + 1] = starts[group] + counts[group];
        }

        var next = starts[..groupsCount];
        var result = new int[links.Length];
        for (var i = 0; i < links.Length; i++)
        {
            result[next[getGroup(links[i])]++] = i;
        }

        return (starts, result);
    }

    // For each group, the number of the distinct other sides of its links (the songs of a loop, the loops of a song).
    private static int[] CountDistinct(CompactStructureLink[] links, int[] starts, int[] groupedLinks, int othersCount, Func<CompactStructureLink, int> getOther)
    {
        var result = new int[starts.Length - 1];
        var lastGroupSeen = new int[othersCount];
        Array.Fill(lastGroupSeen, -1);
        for (var group = 0; group < result.Length; group++)
        {
            for (var i = starts[group]; i < starts[group + 1]; i++)
            {
                ref var lastSeen = ref lastGroupSeen[getOther(links[groupedLinks[i]])];
                if (lastSeen == group) continue;
                lastSeen = group;
                result[group]++;
            }
        }

        return result;
    }

    // The positions of the ids (loops or songs) in the order of their first links.
    private class FirstAppearances
    {
        private readonly int[] _positions;
        private readonly List<int> _numbers = new();

        public FirstAppearances(int[] numbers)
        {
            _positions = new int[numbers.Length == 0 ? 0 : numbers.Max() + 1];
            Array.Fill(_positions, -1);
        }

        public int GetPosition(int number)
        {
            ref var position = ref _positions[number];
            if (position == -1)
            {
                position = _numbers.Count;
                _numbers.Add(number);
            }

            return position;
        }

        public IdPositions ToIdPositions() => new(_numbers.ToArray());
    }
}
