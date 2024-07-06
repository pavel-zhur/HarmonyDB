using Xunit.Abstractions;

namespace HarmonyDB.Index.Analysis.Tests.AiGeneratedKeysDetection;

public class AiGeneratedKeysDetectionProgram(ITestOutputHelper logger)
{
    public void Main()
    {
        var generatorParameters = new TestDataGeneratorParameters();
        var generator = new TestDataGenerator();
        var (songs, loops, loopLinks) = generator.GenerateTestData(generatorParameters);

        // Output initial data
        logger.WriteLine($"Generated {songs.Count} songs and {loops.Count} loops.");
        logger.WriteLine($"First 10 songs:");
        foreach (var song in songs.Take(10))
        {
            var s = song.Value;
            logger.WriteLine($"Song {song.Key}: Known Tonality = {s.KnownTonality}, Is Tonality Known = {s.IsTonalityKnown}, Secret Tonalities = {string.Join(",", s.SecretTonalities)}, Is Known Tonality Incorrect = {s.IsKnownTonalityIncorrect}");
        }

        logger.WriteLine($"First 10 loops:");
        foreach (var loop in loops.Take(10))
        {
            var l = loop.Value;
            logger.WriteLine($"Loop {loop.Key}: Secret Tonalities = {string.Join(",", l.SecretTonalities)}");
        }

        logger.WriteLine($"First 10 loop links:");
        foreach (var link in loopLinks.Take(10))
        {
            logger.WriteLine($"Link: Song {link.SongId}, Loop {link.LoopId}, Shift = {link.Shift}, Count = {link.Count}");
        }

        var analyzer = new MusicAnalyzer(logger, songs.ToDictionary(kv => kv.Key, kv => (ISong)kv.Value), loops.ToDictionary(kv => kv.Key, kv => (ILoop)kv.Value), loopLinks);
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
            logger.WriteLine($"{label}: Min Score = {scoresArray.Min():F4}, Max Score = {scoresArray.Max():F4}, Average Score = {scoresArray.Average():F4}, Median = {Percentile(scoresArray, 0.5):F4}, 90th Percentile = {Percentile(scoresArray, 0.9):F4}");
        }

        logger.WriteLine("\nSummary of Scores:");
        PrintStatistics(allGoodSongs.Select(s => s.Score), "Good Songs");
        PrintStatistics(allBadSongs.Select(s => s.Score), "Bad Songs");
        PrintStatistics(allGoodLoops.Select(l => l.Score), "Good Loops");
        PrintStatistics(allBadLoops.Select(l => l.Score), "Bad Loops");

        // Output bad songs with known tonality, where predicted tonality does not match the known tonality
        logger.WriteLine("\nBad Songs with Incorrectly Known Tonality:");
        int incorrectDetectionCount = 0;
        foreach (var song in incorrectTonalitySongs)
        {
            double[] calculatedProbabilities = analyzer.CalculateProbabilities(song.Id, true);
            int predictedTonality = MusicAnalyzer.GetPredictedTonality(calculatedProbabilities);
            if (!song.SecretTonalities.Contains(predictedTonality))
            {
                incorrectDetectionCount++;
                logger.WriteLine($"Song {song.Id}, Known Tonality: {song.KnownTonality}, Predicted Tonality: {predictedTonality}, Secret Tonalities: {string.Join(",", song.SecretTonalities)}, Score: {song.Score:F4}");
            }
        }
        logger.WriteLine($"Incorrectly Detected Songs with Incorrectly Known Tonality: {incorrectDetectionCount} / {incorrectTonalitySongs.Count()} ({(double)incorrectDetectionCount / incorrectTonalitySongs.Count():P2})");

        // Output the accuracy of detected tonalities for songs and loops
        logger.WriteLine("\nAccuracy of Detected Tonalities:");
        int correctSongDetections = songs.Values.Count(s => s.SecretTonalities.Contains(MusicAnalyzer.GetPredictedTonality(analyzer.CalculateProbabilities(s.Id, true))));
        int correctLoopDetections = loops.Values.Count(l => l.SecretTonalities.Contains(MusicAnalyzer.GetPredictedTonality(analyzer.CalculateProbabilities(l.Id, false))));

        logger.WriteLine($"Correctly Detected Song Tonalities: {correctSongDetections} / {songs.Count} ({(double)correctSongDetections / songs.Count:P2})");
        logger.WriteLine($"Correctly Detected Loop Tonalities: {correctLoopDetections} / {loops.Count} ({(double)correctLoopDetections / loops.Count:P2})");
    }
}