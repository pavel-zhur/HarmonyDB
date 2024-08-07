﻿public class TestDataGenerator
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
            var loopTonalities = isBadLoop ? GenerateRandomTonalities(2, 3) : new[] { (_random.Next(Constants.TonicCount), (Scale)_random.Next(Constants.ScaleCount)) };

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
            var songTonalities = hasModulation ? GenerateRandomTonalities(2, 3) : new[] { (_random.Next(Constants.TonicCount), (Scale)_random.Next(Constants.ScaleCount)) };

            var isKnownTonality = _random.NextDouble() < parameters.KnownTonalityProbability;
            var isKnownTonalityIncorrect = isKnownTonality && _random.NextDouble() < parameters.IncorrectKnownTonalityProbability;

            var knownTonality = isKnownTonality ? songTonalities[_random.Next(songTonalities.Length)] : (-1, Scale.Major);

            // If known tonality is incorrect, assign it a different tonality
            if (isKnownTonalityIncorrect)
            {
                var possibleTonalities = Enumerable.Range(0, Constants.TonicCount).Except(songTonalities.Select(t => t.Item1)).ToArray();
                knownTonality = (possibleTonalities[_random.Next(possibleTonalities.Length)], (Scale)_random.Next(Constants.ScaleCount));
            }

            // Apply mutation to known tonality scale
            if (isKnownTonality && _random.NextDouble() < parameters.SongKnownTonalityScaleMutationProbability)
            {
                knownTonality = (knownTonality.Item1, Constants.GetParallelScale(knownTonality.Item2));
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

            foreach (var songTonality in songTonalities)
            {
                var isLinkMutation = _random.NextDouble() < parameters.LoopLinkScaleMutationProbability;
                var requiredScale = isLinkMutation ? Constants.GetParallelScale(songTonality.Item2) : songTonality.Item2;

                var songLoops = GetRandomLoops(loops.Values.Where(l => l.SecretTonalities.Any(t => t.Item2 == requiredScale)).Select(l => l.Id).ToList(), loopCount);

                foreach (var loopId in songLoops)
                {
                    var loop = loops[loopId];
                    var loopTonality = loop.SecretTonalities.First(t => t.Item2 == requiredScale);

                    var shift = (loopTonality.Item1 - songTonality.Item1 + Constants.TonicCount) % Constants.TonicCount;

                    loopLinks.Add(new()
                    {
                        SongId = songId,
                        LoopId = loopId,
                        Shift = shift,
                        Count = _random.Next(parameters.MinLoopAppearances, parameters.MaxLoopAppearances + 1) // Random count of appearances of the loop in the song
                    });
                }
            }
        }

        return (songs, loops, loopLinks);
    }

    private (int, Scale)[] GenerateRandomTonalities(int min, int max)
    {
        var count = _random.Next(min, max + 1);
        var tonalities = new HashSet<(int, Scale)>();
        while (tonalities.Count < count)
        {
            tonalities.Add((_random.Next(Constants.TonicCount), (Scale)_random.Next(Constants.ScaleCount)));
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

    private (int, Scale) GetRandomTonality((int, Scale)[] tonalities)
    {
        var index = _random.Next(tonalities.Length);
        return tonalities[index];
    }
}
