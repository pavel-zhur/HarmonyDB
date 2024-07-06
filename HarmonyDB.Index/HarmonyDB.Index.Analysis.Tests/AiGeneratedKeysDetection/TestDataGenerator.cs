namespace HarmonyDB.Index.Analysis.Tests.AiGeneratedKeysDetection;

public class TestDataGenerator
{
    private readonly Random _random = new();

    public (Dictionary<string, Song>, Dictionary<string, Loop>, List<LoopLink>) GenerateTestData(TestDataGeneratorParameters parameters)
    {
        var songs = new Dictionary<string, Song>();
        var loops = new Dictionary<string, Loop>();
        var loopLinks = new List<LoopLink>();

        // Generate Loops
        for (var i = 0; i < parameters.TotalLoops; i++)
        {
            var loopId = $"loop{i + 1}";
            var isBadLoop = _random.NextDouble() < parameters.BadCycleProbability;
            var loopTonalities = isBadLoop ? GenerateRandomTonalities(2, 3) : new[] { _random.Next(Constants.TonalityCount) };
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
            var songTonalities = hasModulation ? GenerateRandomTonalities(2, 3) : new[] { _random.Next(Constants.TonalityCount) };
            var isKnownTonality = _random.NextDouble() < parameters.KnownTonalityProbability;
            var isKnownTonalityIncorrect = isKnownTonality && _random.NextDouble() < parameters.IncorrectKnownTonalityProbability;

            var knownTonality = isKnownTonality ? songTonalities[_random.Next(songTonalities.Length)] : -1;

            // If known tonality is incorrect, assign it a different tonality
            if (isKnownTonalityIncorrect)
            {
                var possibleTonalities = Enumerable.Range(0, Constants.TonalityCount).Except(songTonalities).ToArray();
                knownTonality = possibleTonalities[_random.Next(possibleTonalities.Length)];
            }

            songs[songId] = new()
            {
                Id = songId,
                IsTonalityKnown = isKnownTonality,
                KnownTonality = knownTonality,
                SecretTonalities = songTonalities,
                IsKnownTonalityIncorrect = isKnownTonalityIncorrect
            };

            var loopCount = _random.Next(parameters.MinLoopsPerSong, parameters.MaxLoopsPerSong + 1);
            var songLoops = GetRandomLoops(loops.Keys.ToList(), loopCount);

            foreach (var loopId in songLoops)
            {
                var loop = loops[loopId];
                var loopTonality = GetRandomTonality(loop.SecretTonalities);
                var songTonality = songTonalities[_random.Next(songTonalities.Length)];
                var shift = (loopTonality - songTonality + Constants.TonalityCount) % Constants.TonalityCount;

                loopLinks.Add(new()
                {
                    SongId = songId,
                    LoopId = loopId,
                    Shift = shift,
                    Count = _random.Next(parameters.MinLoopAppearances, parameters.MaxLoopAppearances + 1) // Random count of appearances of the loop in the song
                });
            }
        }

        return (songs, loops, loopLinks);
    }

    private int[] GenerateRandomTonalities(int min, int max)
    {
        var count = _random.Next(min, max + 1);
        var tonalities = new HashSet<int>();
        while (tonalities.Count < count)
        {
            tonalities.Add(_random.Next(Constants.TonalityCount));
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

    private int GetRandomTonality(int[] tonalities)
    {
        var index = _random.Next(tonalities.Length);
        return tonalities[index];
    }
}