namespace HarmonyDB.Index.Analysis.Em;

public class TestDataGenerator
{
    private Random random = new Random();

    public (Dictionary<string, Song>, Dictionary<string, Loop>, List<LoopLink>) GenerateTestData(TestDataGeneratorParameters parameters)
    {
        var songs = new Dictionary<string, Song>();
        var loops = new Dictionary<string, Loop>();
        var loopLinks = new List<LoopLink>();

        // Generate Loops
        for (int i = 0; i < parameters.TotalLoops; i++)
        {
            string loopId = $"loop{i + 1}";
            bool isBadLoop = random.NextDouble() < parameters.BadCycleProbability;
            var loopTonalities = isBadLoop ? GenerateRandomTonalities(2, 3) : new[] { (random.Next(Constants.TonicCount), (Scale)random.Next(Constants.ScaleCount)) };

            loops[loopId] = new Loop
            {
                Id = loopId,
                SecretTonalities = loopTonalities
            };
        }

        // Generate Songs
        for (int i = 0; i < parameters.TotalSongs; i++)
        {
            string songId = $"song{i + 1}";
            bool hasModulation = random.NextDouble() < parameters.ModulationProbability;
            var songTonalities = hasModulation ? GenerateRandomTonalities(2, 3) : new[] { (random.Next(Constants.TonicCount), (Scale)random.Next(Constants.ScaleCount)) };

            bool isKnownTonality = random.NextDouble() < parameters.KnownTonalityProbability;
            bool isKnownTonalityIncorrect = isKnownTonality && random.NextDouble() < parameters.IncorrectKnownTonalityProbability;

            var knownTonality = isKnownTonality ? songTonalities[random.Next(songTonalities.Length)] : (-1, Scale.Major);

            // If known tonality is incorrect, assign it a different tonality
            if (isKnownTonalityIncorrect)
            {
                var possibleTonalities = Enumerable.Range(0, Constants.TonicCount).Except(songTonalities.Select(t => t.Item1)).ToArray();
                knownTonality = (possibleTonalities[random.Next(possibleTonalities.Length)], (Scale)random.Next(Constants.ScaleCount));
            }

            // Apply mutation to known tonality scale
            if (isKnownTonality && random.NextDouble() < parameters.SongKnownTonalityScaleMutationProbability)
            {
                knownTonality = (knownTonality.Item1, Constants.GetParallelScale(knownTonality.Item2));
            }

            songs[songId] = new Song
            {
                Id = songId,
                IsTonalityKnown = isKnownTonality,
                KnownTonality = knownTonality,
                SecretTonalities = songTonalities,
                IsKnownTonalityIncorrect = isKnownTonalityIncorrect
            };

            int loopCount = random.Next(parameters.MinLoopsPerSong, parameters.MaxLoopsPerSong + 1);

            foreach (var songTonality in songTonalities)
            {
                bool isLinkMutation = random.NextDouble() < parameters.LoopLinkScaleMutationProbability;
                Scale requiredScale = isLinkMutation ? Constants.GetParallelScale(songTonality.Item2) : songTonality.Item2;

                var songLoops = GetRandomLoops(loops.Values.Where(l => l.SecretTonalities.Any(t => t.Item2 == requiredScale)).Select(l => l.Id).ToList(), loopCount);

                foreach (var loopId in songLoops)
                {
                    var loop = loops[loopId];
                    var loopTonality = loop.SecretTonalities.First(t => t.Item2 == requiredScale);

                    int shift = (loopTonality.Item1 - songTonality.Item1 + Constants.TonicCount) % Constants.TonicCount;

                    loopLinks.Add(new LoopLink
                    {
                        SongId = songId,
                        LoopId = loopId,
                        Shift = shift,
                        Count = random.Next(parameters.MinLoopAppearances, parameters.MaxLoopAppearances + 1) // Random count of appearances of the loop in the song
                    });
                }
            }
        }

        return (songs, loops, loopLinks);
    }

    private (int, Scale)[] GenerateRandomTonalities(int min, int max)
    {
        int count = random.Next(min, max + 1);
        var tonalities = new HashSet<(int, Scale)>();
        while (tonalities.Count < count)
        {
            tonalities.Add((random.Next(Constants.TonicCount), (Scale)random.Next(Constants.ScaleCount)));
        }
        return tonalities.ToArray();
    }

    private List<string> GetRandomLoops(List<string> loopIds, int count)
    {
        var selectedLoops = new List<string>();
        while (selectedLoops.Count < count)
        {
            string loopId = loopIds[random.Next(loopIds.Count)];
            if (!selectedLoops.Contains(loopId))
            {
                selectedLoops.Add(loopId);
            }
        }
        return selectedLoops;
    }

    private (int, Scale) GetRandomTonality((int, Scale)[] tonalities)
    {
        int index = random.Next(tonalities.Length);
        return tonalities[index];
    }
}