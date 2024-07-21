public static class Program
{
    public static void Main(string[] args)
    {
        var generatorParameters = new TestDataGeneratorParameters();
        var generator = new TestDataGenerator();
        var (songs, loops, loopLinks) = generator.GenerateTestData(generatorParameters);

        // Output initial data
        Console.WriteLine($"Generated {songs.Count} songs and {loops.Count} loops.");
        Console.WriteLine("First 10 songs:");
        foreach (var song in songs.Take(10))
        {
            var s = song.Value;
            Console.WriteLine($"Song {song.Key}: Known Tonality = {s.KnownTonality}, Is Tonality Known = {s.IsTonalityKnown}, Secret Tonalities = {string.Join(",", s.SecretTonalities)}, Is Known Tonality Incorrect = {s.IsKnownTonalityIncorrect}");
        }

        Console.WriteLine("First 10 loops:");
        foreach (var loop in loops.Take(10))
        {
            var l = loop.Value;
            Console.WriteLine($"Loop {loop.Key}: Secret Tonalities = {string.Join(",", l.SecretTonalities)}");
        }

        Console.WriteLine("First 10 loop links:");
        foreach (var link in loopLinks.Take(10))
        {
            Console.WriteLine($"Link: Song {link.SongId}, Loop {link.LoopId}, Shift = {link.Shift}, Count = {link.Count}");
        }

        var analyzer = new MusicAnalyzer(songs.ToDictionary(kv => kv.Key, kv => (ISong)kv.Value), loops.ToDictionary(kv => kv.Key, kv => (ILoop)kv.Value), loopLinks);
        analyzer.UpdateProbabilities();

        // Approximate range and average score for good and bad objects
        var allGoodSongs = songs.Values.Where(s => s.SecretTonalities.Length == 1);
        var allBadSongs = songs.Values.Where(s => s.SecretTonalities.Length > 1);
        var incorrectTonalitySongs = songs.Values.Where(s => s.IsKnownTonalityIncorrect);
        var allGoodLoops = loops.Values.Where(l => l.SecretTonalities.Length == 1);
        var allBadLoops = loops.Values.Where(l => l.SecretTonalities.Length > 1);

        // Helper function to calculate percentiles
        double Percentile(IEnumerable<double> sequence, double excelPercentile)
        {
            var elements = sequence.OrderBy(x => x).ToArray();
            var realIndex = excelPercentile * (elements.Length - 1);
            var index = (int)realIndex;
            var frac = realIndex - index;

            if (index + 1 < elements.Length)
                return elements[index] * (1 - frac) + elements[index + 1] * frac;
            else
                return elements[index];
        }

        void PrintStatistics(IEnumerable<double> scores, string label)
        {
            var scoresArray = scores.ToArray();
            Console.WriteLine($"{label}: Min Score = {scoresArray.Min():F4}, Max Score = {scoresArray.Max():F4}, Average Score = {scoresArray.Average():F4}, Median = {Percentile(scoresArray, 0.5):F4}, 90th Percentile = {Percentile(scoresArray, 0.9):F4}");
        }

        Console.WriteLine("\nSummary of Scores:");
        PrintStatistics(allGoodSongs.Select(s => s.Score.TonicScore), "Good Songs (Tonic)");
        PrintStatistics(allGoodSongs.Select(s => s.Score.ScaleScore), "Good Songs (Scale)");
        PrintStatistics(allBadSongs.Select(s => s.Score.TonicScore), "Bad Songs (Tonic)");
        PrintStatistics(allBadSongs.Select(s => s.Score.ScaleScore), "Bad Songs (Scale)");
        PrintStatistics(allGoodLoops.Select(l => l.Score.TonicScore), "Good Loops (Tonic)");
        PrintStatistics(allGoodLoops.Select(l => l.Score.ScaleScore), "Good Loops (Scale)");
        PrintStatistics(allBadLoops.Select(l => l.Score.TonicScore), "Bad Loops (Tonic)");
        PrintStatistics(allBadLoops.Select(l => l.Score.ScaleScore), "Bad Loops (Scale)");

        // Output bad songs with known tonality, where predicted tonality does not match the known tonality
        Console.WriteLine("\nBad Songs with Incorrectly Known Tonality:");
        var incorrectDetectionCount = 0;
        foreach (var song in incorrectTonalitySongs)
        {
            var calculatedProbabilities = analyzer.CalculateProbabilities(song.Id, true);
            var predictedTonality = MusicAnalyzer.GetPredictedTonality(calculatedProbabilities);
            if (!song.SecretTonalities.Contains(predictedTonality))
            {
                incorrectDetectionCount++;
                Console.WriteLine($"Song {song.Id}, Known Tonality: {song.KnownTonality}, Predicted Tonality: {predictedTonality}, Secret Tonalities: {string.Join(",", song.SecretTonalities)}, Score: {song.Score}");
            }
        }
        Console.WriteLine($"Incorrectly Detected Songs with Incorrectly Known Tonality: {incorrectDetectionCount} / {incorrectTonalitySongs.Count()} ({(double)incorrectDetectionCount / incorrectTonalitySongs.Count():P2})");

        // Output the accuracy of detected tonalities for songs and loops
        Console.WriteLine("\nAccuracy of Detected Tonalities:");
        var correctSongDetections = songs.Values.Count(s => s.SecretTonalities.Contains(MusicAnalyzer.GetPredictedTonality(analyzer.CalculateProbabilities(s.Id, true))));
        var correctLoopDetections = loops.Values.Count(l => l.SecretTonalities.Contains(MusicAnalyzer.GetPredictedTonality(analyzer.CalculateProbabilities(l.Id, false))));

        Console.WriteLine($"Correctly Detected Song Tonalities: {correctSongDetections} / {songs.Count} ({(double)correctSongDetections / songs.Count:P2})");
        Console.WriteLine($"Correctly Detected Loop Tonalities: {correctLoopDetections} / {loops.Count} ({(double)correctLoopDetections / loops.Count:P2})");

        // Output the accuracy of detected tonics for songs and loops
        Console.WriteLine("\nAccuracy of Detected Tonics:");
        var correctSongTonicDetections = songs.Values.Count(s => s.SecretTonalities.Any(t => t.Item1 == MusicAnalyzer.GetPredictedTonality(analyzer.CalculateProbabilities(s.Id, true)).Item1));
        var correctLoopTonicDetections = loops.Values.Count(l => l.SecretTonalities.Any(t => t.Item1 == MusicAnalyzer.GetPredictedTonality(analyzer.CalculateProbabilities(l.Id, false)).Item1));

        Console.WriteLine($"Correctly Detected Song Tonics: {correctSongTonicDetections} / {songs.Count} ({(double)correctSongTonicDetections / songs.Count:P2})");
        Console.WriteLine($"Correctly Detected Loop Tonics: {correctLoopTonicDetections} / {loops.Count} ({(double)correctLoopTonicDetections / loops.Count:P2})");
    }
}
