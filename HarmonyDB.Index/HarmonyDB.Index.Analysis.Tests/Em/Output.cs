using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Em.Services;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public class Output(ILogger<Output> output, MusicAnalyzer analyzer)
{
    public void TraceInput(TestEmModel emModel, List<TestLoopLink> links)
    {
        // Output initial data
        output.LogInformation($"Generated {emModel.Songs.Count} songs and {emModel.Loops.Count} loops.");
        output.LogInformation("First 10 songs:");
        foreach (var song in emModel.Songs.Take(10))
        {
            output.LogInformation($"Song {song.Id}: Known Tonality = {song.KnownTonality}, Secret Tonalities = {string.Join(",", song.SecretTonalities)}, Is Known Tonality Incorrect = {song.IsKnownTonalityIncorrect}");
        }

        output.LogInformation("First 10 loops:");
        foreach (var loop in emModel.Loops.Take(10))
        {
            output.LogInformation($"Loop {loop.Id}: Secret Tonalities = {string.Join(",", loop.SecretTonalities)}");
        }

        output.LogInformation("First 10 loop links:");
        foreach (var link in links.Take(10))
        {
            output.LogInformation($"Link: Song {link.SongId}, Loop {link.LoopId}, Shift = {link.Shift}, Weight = {link.Weight}");
        }
    }

    public void TraceOutput(TestEmModel emModel, EmContext emContext)
    {
        // Approximate range and average score for good and bad objects
        var allGoodSongs = emModel.Songs.Where(s => s.SecretTonalities.Length == 1);
        var allBadSongs = emModel.Songs.Where(s => s.SecretTonalities.Length > 1);
        var incorrectTonalitySongs = emModel.Songs.Where(s => s.IsKnownTonalityIncorrect);
        var allGoodLoops = emModel.Loops.Where(l => l.SecretTonalities.Length == 1);
        var allBadLoops = emModel.Loops.Where(l => l.SecretTonalities.Length > 1);

        // Helper function to calculate percentiles
        double Percentile(IEnumerable<float> sequence, double excelPercentile)
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

        void PrintStatistics(IEnumerable<float> scores, string label)
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
            var calculatedProbabilities = analyzer.CalculateProbabilities(emContext, song.Id, true);
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
        var correctSongDetections = emModel.Songs.Count(s => s.SecretTonalities.Contains(analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(emContext, s.Id, true))));
        var correctLoopDetections = emModel.Loops.Count(l => l.SecretTonalities.Contains(analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(emContext, l.Id, false))));

        output.LogInformation($"Correctly Detected Song Tonalities: {correctSongDetections} / {emModel.Songs.Count} ({(double)correctSongDetections / emModel.Songs.Count:P2})");
        output.LogInformation($"Correctly Detected Loop Tonalities: {correctLoopDetections} / {emModel.Loops.Count} ({(double)correctLoopDetections / emModel.Loops.Count:P2})");

        // Output the accuracy of detected tonics for songs and loops
        output.LogInformation("\nAccuracy of Detected Tonics:");
        var correctSongTonicDetections = emModel.Songs.Count(s => s.SecretTonalities.Any(t => Constants.GetMajorTonic(t) == Constants.GetMajorTonic(analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(emContext, s.Id, true)))));
        var correctLoopTonicDetections = emModel.Loops.Count(l => l.SecretTonalities.Any(t => Constants.GetMajorTonic(t) == Constants.GetMajorTonic(analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(emContext, l.Id, false)))));

        output.LogInformation($"Correctly Detected Song Tonics: {correctSongTonicDetections} / {emModel.Songs.Count} ({(double)correctSongTonicDetections / emModel.Songs.Count:P2})");
        output.LogInformation($"Correctly Detected Loop Tonics: {correctLoopTonicDetections} / {emModel.Loops.Count} ({(double)correctLoopTonicDetections / emModel.Loops.Count:P2})");
    }
}