﻿using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.Analysis.Em.Services;

public class Output(ILogger<Output> output, MusicAnalyzer analyzer)
{
    public void TraceInput(Dictionary<string, Song> songs, Dictionary<string, Loop> loops, List<LoopLink> loopLinks)
    {
        // Output initial data
        output.LogInformation($"Generated {songs.Count} songs and {loops.Count} loops.");
        output.LogInformation($"First 10 songs:");
        foreach (var song in songs.Take(10))
        {
            var s = song.Value;
            output.LogInformation($"Song {song.Key}: Known Tonality = {s.KnownTonality}, Is Tonality Known = {s.IsTonalityKnown}, Secret Tonalities = {string.Join(",", s.SecretTonalities)}, Is Known Tonality Incorrect = {s.IsKnownTonalityIncorrect}");
        }

        output.LogInformation($"First 10 loops:");
        foreach (var loop in loops.Take(10))
        {
            var l = loop.Value;
            output.LogInformation($"Loop {loop.Key}: Secret Tonalities = {string.Join(",", l.SecretTonalities)}");
        }

        output.LogInformation($"First 10 loop links:");
        foreach (var link in loopLinks.Take(10))
        {
            output.LogInformation($"Link: Song {link.SongId}, Loop {link.LoopId}, Shift = {link.Shift}, Count = {link.Count}");
        }
    }

    public void TraceOutput(Dictionary<string, Song> songs, Dictionary<string, Loop> loops)
    {
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
        int incorrectDetectionCount = 0;
        foreach (var song in incorrectTonalitySongs)
        {
            double[,] calculatedProbabilities = analyzer.CalculateProbabilities(song.Id, true);
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
        int correctSongDetections = songs.Values.Count(s => s.SecretTonalities.Contains(analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(s.Id, true))));
        int correctLoopDetections = loops.Values.Count(l => l.SecretTonalities.Contains(analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(l.Id, false))));

        output.LogInformation($"Correctly Detected Song Tonalities: {correctSongDetections} / {songs.Count} ({(double)correctSongDetections / songs.Count:P2})");
        output.LogInformation($"Correctly Detected Loop Tonalities: {correctLoopDetections} / {loops.Count} ({(double)correctLoopDetections / loops.Count:P2})");

        // Output the accuracy of detected tonics for songs and loops
        output.LogInformation("\nAccuracy of Detected Tonics:");
        int correctSongTonicDetections = songs.Values.Count(s => s.SecretTonalities.Any(t => t.Item1 == analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(s.Id, true)).Item1));
        int correctLoopTonicDetections = loops.Values.Count(l => l.SecretTonalities.Any(t => t.Item1 == analyzer.GetPredictedTonality(analyzer.CalculateProbabilities(l.Id, false)).Item1));

        output.LogInformation($"Correctly Detected Song Tonics: {correctSongTonicDetections} / {songs.Count} ({(double)correctSongTonicDetections / songs.Count:P2})");
        output.LogInformation($"Correctly Detected Loop Tonics: {correctLoopTonicDetections} / {loops.Count} ({(double)correctLoopTonicDetections / loops.Count:P2})");
    }
}