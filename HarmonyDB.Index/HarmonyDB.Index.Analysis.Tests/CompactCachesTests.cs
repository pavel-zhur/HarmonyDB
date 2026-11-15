using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Em;
using HarmonyDB.Index.Analysis.Models.Ids;
using HarmonyDB.Index.Analysis.Models.Structure;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Analysis.Tools;

namespace HarmonyDB.Index.Analysis.Tests;

public class CompactCachesTests(InputParser inputParser, ProgressionsBuilder progressionsBuilder, ChordDataParser chordDataParser, ProgressionsSearch progressionsSearch)
{
    [Fact]
    public void StructuresGiveWhatTheLinksDid()
    {
        var random = new Random(1);
        var loops = Enumerable.Range(0, 50).Select(_ => RandomMovements(random, random.Next(2, 6)).SerializeLoop()).Distinct().ToList();
        var links = Enumerable.Range(0, 300)
            .SelectMany(s => Enumerable.Range(0, random.Next(1, 8)).Select(_ => new StructureLink(
                loops[random.Next(loops.Count)],
                $"song-{s}",
                (byte)random.Next(12),
                random.NextSingle() * 10,
                random.NextSingle() * 5,
                random.NextSingle())))
            .OrderBy(_ => random.Next())
            .ToList();

        var ids = new IndexIds();
        ids.Songs.GetOrAdd("another song, met before");
        var structures = Structures.Read(new MemoryStream(Structures.Serialize(links)), ids);

        Assert.Equal(links, structures.Links.ToArray().Select(structures.ToStructureLink));

        var expectedLoops = links.GroupBy(x => x.Normalized).ToList();
        Assert.Equal(expectedLoops.Select(g => g.Key), Enumerable.Range(0, structures.Loops.Count).Select(p => ids.Loops[structures.Loops.GetNumber(p)]));
        foreach (var g in expectedLoops)
        {
            var loop = ids.Loops.GetNumber(g.Key);
            Assert.Equal(
                new StructureLoop(g.Key, g.Key.DeserializeLoop().Length, g.Sum(x => x.Occurrences), g.Sum(x => x.Successions), g.Average(x => x.Coverage), g.Select(x => x.ExternalId).Distinct().Count()),
                structures.GetLoop(loop));
            Assert.Equal(g, structures.GetLinksByLoop(loop).Select(structures.ToStructureLink));
        }

        var expectedSongs = links.GroupBy(x => x.ExternalId).ToList();
        Assert.Equal(expectedSongs.Select(g => g.Key), Enumerable.Range(0, structures.Songs.Count).Select(p => ids.Songs[structures.Songs.GetNumber(p)]));
        foreach (var g in expectedSongs)
        {
            var song = ids.Songs.GetNumber(g.Key);
            Assert.Equal(new StructureSong(g.Key, g.Select(x => x.Normalized).Distinct().Count()), structures.GetSong(song));
            Assert.Equal(g, structures.GetLinksBySong(song).Select(structures.ToStructureLink));
        }

        var other = ids.Songs.GetNumber("another song, met before");
        Assert.False(structures.TryGetSong(other, out _));
        Assert.Throws<KeyNotFoundException>(() => structures.GetSong(other));
        Assert.Empty(structures.GetLinksBySong(other));
    }

    [Fact]
    public void TonalitiesGiveWhatTheModelDid()
    {
        var random = new Random(2);
        var loops = Enumerable.Range(0, 30).Select(i => RandomSource(random, new Loop { Id = $"loop-{i}" })).ToList();
        var songs = Enumerable.Range(0, 40).Select(i => RandomSource(random, new Song
        {
            Id = $"song-{i}",
            KnownTonality = random.Next(3) == 0 ? null : ((byte)random.Next(12), (Scale)random.Next(2)),
        })).ToList();

        var ids = new IndexIds();
        var tonalities = Tonalities.Read(new MemoryStream(new EmModel(songs, loops).Serialize()), ids);

        Assert.Equal(loops.Select(x => x.Id), Enumerable.Range(0, tonalities.Loops.Count).Select(tonalities.Loops.GetId));
        Assert.Equal(songs.Select(x => x.Id), Enumerable.Range(0, tonalities.Songs.Count).Select(tonalities.Songs.GetId));
        for (var i = 0; i < loops.Count; i++)
        {
            Assert.Equal(loops[i].TonalityProbabilities.ToLinear(), tonalities.Loops.GetProbabilities(i));
            Assert.Equal(loops[i].Score, tonalities.Loops.GetScore(i));
            Assert.Equal(i, tonalities.Loops.Positions.GetPosition(ids.Loops.GetNumber(loops[i].Id)));
        }

        for (var i = 0; i < songs.Count; i++)
        {
            Assert.Equal(songs[i].TonalityProbabilities.ToLinear(), tonalities.Songs.GetProbabilities(i));
            Assert.Equal(songs[i].Score, tonalities.Songs.GetScore(i));
            Assert.Equal(songs[i].KnownTonality, tonalities.Songs.GetKnownTonality(i));
            Assert.Equal(i, tonalities.Songs.Positions.GetPosition(ids.Songs.GetNumber(songs[i].Id)));
        }
    }

    [Fact]
    public void ProgressionsKeepTheMovements()
    {
        var random = new Random(3);

        // enough movements to fill several blocks, and a sequence longer than a block
        var progressions = Enumerable.Range(0, 3000)
            .Select(_ => new CompactChordsProgression
            {
                HarmonySequenceLength = random.Next(1000),
                ExtendedHarmonyMovementsSequences = Enumerable.Range(0, random.Next(4))
                    .Select(_ => new CompactHarmonyMovementsSequence
                    {
                        FirstMovementFromIndex = random.Next(100),
                        FirstRoot = (byte)random.Next(12),
                        Movements = RandomMovements(random, random.Next(1000)),
                    })
                    .ToList(),
            })
            .Append(new()
            {
                HarmonySequenceLength = 5,
                ExtendedHarmonyMovementsSequences = [new() { FirstMovementFromIndex = 0, FirstRoot = 1, Movements = RandomMovements(random, 1_100_000) }],
            })
            .ToList();

        var ids = new IndexIds();
        var builder = new CompactChordsProgressions.Builder(ids.Songs);
        for (var i = 0; i < progressions.Count; i++)
        {
            builder.Add($"song-{i}", progressions[i].Serialize().ToArray());
        }

        var compact = builder.Build();

        Assert.Equal(progressions.Count, compact.Count);
        for (var i = 0; i < progressions.Count; i++)
        {
            Assert.Equal($"song-{i}", compact.GetExternalId(i));
            Assert.Equal(progressions[i].Serialize().ToArray(), compact.Get(i).Serialize().ToArray());
        }
    }

    [Fact]
    public void ProgressionsAreFoundAsBefore()
    {
        var progressions = new[]
            {
                "Am F C G Am F C G Em",
                "C G Am F C G Am F",
                "Dm G C Am Dm G C",
                "Am Dm E Am Dm E Am",
                "C Am F G C",
                "Em C G D Em C",
                "Csus2 G5 Am Fsus4 C",
                "F#m D A E F#m D A E",
            }
            .Select(x => progressionsBuilder.BuildProgression(x.Split(' ').Select(inputParser.ParseChord).ToList()))
            .ToList();

        var ids = new IndexIds();
        var builder = new CompactChordsProgressions.Builder(ids.Songs);
        for (var i = 0; i < progressions.Count; i++)
        {
            builder.Add($"song-{i}", progressions[i].Compact().Serialize().ToArray());
        }

        var compact = builder.Build();

        foreach (var query in new[] { "Am F C G", "C G Am F C", "Em C", "Am Dm E Am", "C Am", "F#m D A E F#m" })
        {
            var searchPhrase = inputParser.ParseSequence(query);
            var expected = progressionsSearch.Search(progressions, searchPhrase).foundProgressionsWithCoverage;
            var found = progressionsSearch.Search(compact, Enumerable.Range(0, compact.Count), searchPhrase);
            Assert.Equal(
                progressions.Select((p, i) => (i, expected.TryGetValue(p, out var coverage) ? coverage : (float?)null)).Where(x => x.Item2.HasValue),
                found.Select(x => (x.Key, (float?)x.Value)));
        }
    }

    private static ReadOnlyMemory<CompactHarmonyMovement> RandomMovements(Random random, int length)
        => Enumerable.Range(0, length)
            .Select(_ => new CompactHarmonyMovement
            {
                RootDelta = (byte)random.Next(12),
                FromType = (ChordType)random.Next(8),
                ToType = (ChordType)random.Next(8),
            })
            .ToArray();

    private static T RandomSource<T>(Random random, T source)
        where T : ISource
    {
        source.Score = (random.NextSingle(), random.NextSingle());
        for (var i = 0; i < Constants.TonicCount; i++)
        {
            for (var j = 0; j < Constants.ScaleCount; j++)
            {
                source.TonalityProbabilities[i, j] = random.NextSingle();
            }
        }

        return source;
    }
}
