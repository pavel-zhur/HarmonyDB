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
            int[] loopTonalities = isBadLoop ? GenerateRandomTonalities(2, 3) : new[] { random.Next(Constants.TonalityCount) };
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
            int[] songTonalities = hasModulation ? GenerateRandomTonalities(2, 3) : new[] { random.Next(Constants.TonalityCount) };
            bool isKnownTonality = random.NextDouble() < parameters.KnownTonalityProbability;
            bool isKnownTonalityIncorrect = isKnownTonality && random.NextDouble() < parameters.IncorrectKnownTonalityProbability;

            int knownTonality = isKnownTonality ? songTonalities[random.Next(songTonalities.Length)] : -1;

            // If known tonality is incorrect, assign it a different tonality
            if (isKnownTonalityIncorrect)
            {
                var possibleTonalities = Enumerable.Range(0, Constants.TonalityCount).Except(songTonalities).ToArray();
                knownTonality = possibleTonalities[random.Next(possibleTonalities.Length)];
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
            var songLoops = GetRandomLoops(loops.Keys.ToList(), loopCount);

            foreach (var loopId in songLoops)
            {
                var loop = loops[loopId];
                int loopTonality = GetRandomTonality(loop.SecretTonalities);
                int songTonality = songTonalities[random.Next(songTonalities.Length)];
                int shift = (loopTonality - songTonality + Constants.TonalityCount) % Constants.TonalityCount;

                loopLinks.Add(new LoopLink
                {
                    SongId = songId,
                    LoopId = loopId,
                    Shift = shift,
                    Count = random.Next(parameters.MinLoopAppearances, parameters.MaxLoopAppearances + 1) // Random count of appearances of the loop in the song
                });
            }
        }

        return (songs, loops, loopLinks);
    }

    private int[] GenerateRandomTonalities(int min, int max)
    {
        int count = random.Next(min, max + 1);
        var tonalities = new HashSet<int>();
        while (tonalities.Count < count)
        {
            tonalities.Add(random.Next(Constants.TonalityCount));
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

    private int GetRandomTonality(int[] tonalities)
    {
        int index = random.Next(tonalities.Length);
        return tonalities[index];
    }
}