using HarmonyDB.Index.Analysis.Em.Services;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public class Output(ILogger<Output> output, MusicAnalyzer analyzer)
{
    public void TraceInput(TestEmModel emModel)
    {
        // Output initial data
        output.LogInformation($"Generated {emModel.Songs.Count} songs and {emModel.Loops.Count} loops.");
        output.LogInformation("First 10 songs:");
        foreach (var song in emModel.Songs.Take(10))
        {
            var s = song.Value;
            output.LogInformation($"Song {song.Key}: Known Tonality = {s.KnownTonality}, Is Tonality Known = {s.IsTonalityKnown}, Secret Tonalities = {string.Join(",", s.SecretTonalities)}, Is Known Tonality Incorrect = {s.IsKnownTonalityIncorrect}");
        }

        output.LogInformation("First 10 loops:");
        foreach (var loop in emModel.Loops.Take(10))
        {
            var l = loop.Value;
            output.LogInformation($"Loop {loop.Key}: Secret Tonalities = {string.Join(",", l.SecretTonalities)}");
        }

        output.LogInformation("First 10 loop links:");
        foreach (var link in emModel.LoopLinks.Take(10))
        {
            output.LogInformation($"Link: Song {link.SongId}, Loop {link.LoopId}, Shift = {link.Shift}, Count = {link.Count}");
        }
    }

    public void TraceOutput(TestEmModel emModel)
    {
        // Approximate range and average score for good and bad objects
        var allGoodSongs = emModel.Songs.Values.Where(s => s.SecretTonalities.Length == 1);
        var allBadSongs = emModel.Songs.Values.Where(s => s.SecretTonalities.Length > 1);
        var incorrectTonalitySongs = emModel.Songs.Values.Where(s => s.IsKnownTonalityIncorrect);
        var allGoodLoops = emModel.Loops.Values.Where(l => l.SecretTonalities.Length == 1);
        var allBadLoops = emModel.Loops.Values.Where(l => l.SecretTonalities.Length > 1);

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
            output.LogInformation($"{label}: Min Score = {scoresArray.Min():F4}, Max Score = {scoresArray.Max():F4}, Average Score = {scoresArray.Average():F4}, Median = {Percentile(scoresArray, 0.5):F4}, 90th Percentile = {Percentile(scoresArray, 0.9):F4}");
        }

        output.LogInformation("\nSummary of Scores:");
        PrintStatistics(allGoodSongs.Select(s => s.Score.TonicScore), "Good Songs (Tonic)");
        PrintStatistics(allGoodSongs.Select(s => s.Score.ScaleScore), "Good Songs (Scale)");
        PrintStatistics(allBadSongs.Select(s => s.Score.TonicScore), "Bad Songs (Tonic)");
        PrintStatistics(allBadSongs.Select(s => s.Score.ScaleScore), "Bad Songs (Scale)");
        PrintStatistics(allGoodLoops.Select(l => l.Score.TonicScore), "Good Loops (Tonic)");
        PrintStatistics(allGoodLoops.Select(l => l.Score.ScaleScore), "Good Loops (Scale)");
        PrintStatistics(allBadLoops.Select(l => l.Score.TonicScore), "Bad Loops (Tonic)");
        PrintStatistics(allBadLoops.Select(l => l.Score.ScaleScore), "Bad Loops (Scale)");

        // Output bad songs with known tonality, where predicted tonality does not match the known tonality
        output.LogInformation("\nBad Songs with Incorrectly Known Tonality:");
        var incorrectDetectionCount = 0;
        foreach (var song in incorrectTonalitySongs)
        {
            var calculatedProbabilities = analyzer.CalculateProbabilities(emModel, song.Id, true);
            var predictedTonality = analyzer.GetPredictedTonality(calculatedProbabilities);
            if (!song.SecretTonalities.Contains(predictedTonality))
            {
                incorrectDetectionCount++;
                output.LogInformation($"Song {song.Id}, Known Tonality: {song.KnownTonality}, Predicted Tonality: {predictedTonality}, Secret Tonalities: {string.Join(",", song.SecretTonalities)}, Score: {song.Score}");
            }
        }
        output.LogInformation($"Incorrectly Detected Songs with Incorrectly Known Tonality: {incorrectDetectionCount} / {incorrectTonalitySongs.Count()} ({(double)incorrectDetectionCount / incorrectTonalitySongs.Count():P2})");

        // Output the accuracy of detected tonalities for songs and loops
        output.LogInformation("\nAccuracy of Detected Tonalities:");
        var correctSongDetections = emModel.Songs.Values.Count(s => s.SecretTonalities.Contains(analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(emModel, s.Id, true))));
        var correctLoopDetections = emModel.Loops.Values.Count(l => l.SecretTonalities.Contains(analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(emModel, l.Id, false))));

        output.LogInformation($"Correctly Detected Song Tonalities: {correctSongDetections} / {emModel.Songs.Count} ({(double)correctSongDetections / emModel.Songs.Count:P2})");
        output.LogInformation($"Correctly Detected Loop Tonalities: {correctLoopDetections} / {emModel.Loops.Count} ({(double)correctLoopDetections / emModel.Loops.Count:P2})");

        // Output the accuracy of detected tonics for songs and loops
        output.LogInformation("\nAccuracy of Detected Tonics:");
        var correctSongTonicDetections = emModel.Songs.Values.Count(s => s.SecretTonalities.Any(t => t.Item1 == analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(emModel, s.Id, true)).Item1));
        var correctLoopTonicDetections = emModel.Loops.Values.Count(l => l.SecretTonalities.Any(t => t.Item1 == analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(emModel, l.Id, false)).Item1));

        output.LogInformation($"Correctly Detected Song Tonics: {correctSongTonicDetections} / {emModel.Songs.Count} ({(double)correctSongTonicDetections / emModel.Songs.Count:P2})");
        output.LogInformation($"Correctly Detected Loop Tonics: {correctLoopTonicDetections} / {emModel.Loops.Count} ({(double)correctLoopTonicDetections / emModel.Loops.Count:P2})");
    }
}