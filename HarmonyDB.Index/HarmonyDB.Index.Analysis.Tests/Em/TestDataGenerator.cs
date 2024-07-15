using HarmonyDB.Index.Analysis.Em;
using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Models.Em;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public class TestDataGenerator
{
    private readonly Random _random = new();

    public (TestEmModel emModel, List<LoopLink> links) GenerateTestData(
        TestDataGeneratorParameters parameters)
    {
        var songs = new Dictionary<string, TestSong>();
        var loops = new Dictionary<string, TestLoop>();
        var loopLinks = new List<LoopLink>();

        // Generate Loops
        for (var i = 0; i < parameters.TotalLoops; i++)
        {
            var loopId = $"loop{i + 1}";
            var isBadLoop = _random.NextDouble() < parameters.BadCycleProbability;
            var loopTonalities = isBadLoop
                ? GenerateRandomTonalities(2, 3)
                : new[] { ((byte)_random.Next(Constants.TonicCount), (Scale)_random.Next(Constants.ScaleCount)) };

            loops[loopId] = new()
            {
                Id = loopId,
                SecretTonalities = loopTonalities
            };
        }

        // Generate Songs
        for (var i = 0; i < parameters.TotalSongs; i++)
        {
            var songId = $"song{i + 1}";
            var hasModulation = _random.NextDouble() < parameters.ModulationProbability;
            var songTonalities = hasModulation
                ? GenerateRandomTonalities(2, 3)
                : [(tonic: (byte)_random.Next(Constants.TonicCount), scale: (Scale)_random.Next(Constants.ScaleCount))];

            var knownTonality = _random.NextDouble() < parameters.KnownTonalityProbability ? songTonalities[_random.Next(songTonalities.Length)] : ((byte tonic, Scale scale)?)null;
            var isKnownTonalityIncorrect = knownTonality.HasValue && _random.NextDouble() < parameters.IncorrectKnownTonalityProbability;

            // If known tonality is incorrect, assign it a different tonality
            if (isKnownTonalityIncorrect)
            {
                var possibleTonalities = Enumerable.Range(0, Constants.TonicCount).Select(x => (byte)x)
                    .Except(songTonalities.Select(t => t.Item1)).ToArray();
                knownTonality = (possibleTonalities[_random.Next(possibleTonalities.Length)],
                    (Scale)_random.Next(Constants.ScaleCount));
            }

            var song = new TestSong
            {
                Id = songId,
                KnownTonality = !knownTonality.HasValue
                    ? null
                    : !isKnownTonalityIncorrect && _random.NextDouble() < parameters.SongKnownTonalityScaleMutationProbability
                        ? Constants.GetParallelScale(knownTonality.Value, true) // Apply mutation to known tonality scale
                        : knownTonality,
                SecretTonalities = songTonalities,
                IsKnownTonalityIncorrect = isKnownTonalityIncorrect
            };

            songs[songId] = song;

            var loopCount = _random.Next(parameters.MinLoopsPerSong, parameters.MaxLoopsPerSong + 1);

            foreach (var songTonality in songTonalities)
            {
                var isLinkMutation = _random.NextDouble() < parameters.LoopLinkScaleMutationProbability;
                var requiredScale =
                    isLinkMutation ? Constants.GetParallelScale(songTonality, true).scale : songTonality.Item2;

                var songLoops =
                    GetRandomLoops(
                        loops.Values.Where(l => l.SecretTonalities.Any(t => t.Item2 == requiredScale)).Select(l => l.Id)
                            .ToList(), loopCount);

                foreach (var loopId in songLoops)
                {
                    var loop = loops[loopId];
                    var loopTonality = loop.SecretTonalities.First(t => t.Item2 == requiredScale);

                    var shift = loopTonality.Scale == songTonality.scale
                        ? (loopTonality.Tonic + songTonality.tonic) % Constants.TonicCount
                        : (loopTonality.Tonic + Constants.GetParallelScale(songTonality, true).tonic) % Constants.TonicCount;

                    loopLinks.Add(new()
                    {
                        Song = song,
                        Loop = loop,
                        Shift = (byte)shift,
                        Weight = _random.Next(parameters.MinLoopAppearances,
                            parameters.MaxLoopAppearances + 1) // Random count of appearances of the loop in the song
                    });
                }
            }
        }

        return (new(songs.Values, loops.Values), loopLinks);
    }

    private (byte tonic, Scale scale)[] GenerateRandomTonalities(int min, int max)
    {
        var count = _random.Next(min, max + 1);
        var tonalities = new HashSet<(byte, Scale)>();
        while (tonalities.Count < count)
        {
            tonalities.Add(((byte)_random.Next(Constants.TonicCount), (Scale)_random.Next(Constants.ScaleCount)));
        }

        return tonalities.ToArray();
    }

    private List<string> GetRandomLoops(List<string> loopIds, int count)
    {
        var selectedLoops = new List<string>();
        while (selectedLoops.Count < count)
        {
            var loopId = loopIds[_random.Next(loopIds.Count)];
            if (!selectedLoops.Contains(loopId))
            {
                selectedLoops.Add(loopId);
            }
        }

        return selectedLoops;
    }
}