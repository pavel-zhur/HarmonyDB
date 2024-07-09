using HarmonyDB.Index.Analysis.Em.Models;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.Analysis.Em.Services;

public class MusicAnalyzer(ILogger<MusicAnalyzer> logger)
{
    public EmContext CreateContext(IEmModel input)
    {
        var loopLinksByLoopId = input.LoopLinks.ToLookup(x => x.LoopId);
        return new()
        {
            LoopLinksByLoopId = loopLinksByLoopId,
            LoopLinksBySongId = input.LoopLinks.ToLookup(x => x.SongId),
            SongCounts = loopLinksByLoopId.ToDictionary(x => x.Key, x => x.Select(x => x.SongId).Distinct().Count()),
        };
    }

    public void UpdateProbabilities(IEmModel emModel, EmContext emContext)
    {
        InitializeProbabilities(emModel);

        const double tolerance = 0.01;
        const int stabilityCheckWindow = 5; // Number of iterations to check for stability
        const double stabilityThreshold = 1e-6; // Threshold for considering changes as stable
        var recentMaxChanges = new List<double>();
        var hasConverged = false;
        var iterationCount = 0;

        while (!hasConverged)
        {
            iterationCount++;
            var maxChange = 0.0;
            var maxChangeLockObject = new object();

            Parallel.ForEach(emModel.Loops, loop =>
            {
                var newProbabilities = CalculateProbabilities(emContext, loop.Id, false);
                var change = CalculateMaxChange(loop.TonalityProbabilities, newProbabilities);
                lock (maxChangeLockObject)
                {
                    maxChange = Math.Max(maxChange, change);
                }

                loop.TonalityProbabilities = newProbabilities;
                loop.Score = CalculateEntropy(emContext, loop.Id, false);
            });

            Parallel.ForEach(emModel.Songs, song =>
            {
                if (!song.IsTonalityKnown)
                {
                    var newProbabilities = CalculateProbabilities(emContext, song.Id, true);
                    var change = CalculateMaxChange(song.TonalityProbabilities, newProbabilities);
                    lock (maxChangeLockObject)
                    {
                        maxChange = Math.Max(maxChange, change);
                    }

                    song.TonalityProbabilities = newProbabilities;
                }

                song.Score = CalculateEntropy(emContext, song.Id, true);
            });

            recentMaxChanges.Add(maxChange);
            if (recentMaxChanges.Count > stabilityCheckWindow)
            {
                recentMaxChanges.RemoveAt(0);
            }

            if (recentMaxChanges.Count == stabilityCheckWindow)
            {
                var minChange = recentMaxChanges.Min();
                var maxInWindow = recentMaxChanges.Max();

                if (maxInWindow - minChange < stabilityThreshold)
                {
                    throw new("Algorithm is oscillating without converging.");
                }
            }

            if (maxChange < tolerance)
            {
                hasConverged = true;
            }

            logger.LogInformation($"Iteration {iterationCount}, Max Change: {maxChange:F6}");
        }

        logger.LogInformation("Converged after " + iterationCount + " iterations.");

        Parallel.ForEach(emModel.Songs, song =>
        {
            if (song.IsTonalityKnown)
            {
                song.TonalityProbabilities = CalculateProbabilities(emContext, song.Id, true);
            }
        });
    }

    private void InitializeProbabilities(IEmModel emModel)
    {
        foreach (var song in emModel.Songs)
        {
            if (song.IsTonalityKnown)
            {
                for (var i = 0; i < Constants.TonicCount; i++)
                {
                    for (var j = 0; j < Constants.ScaleCount; j++)
                    {
                        song.TonalityProbabilities[i, j] = i == song.KnownTonality.Item1 && j == (int)song.KnownTonality.Item2 ? 1.0 : 0.0;
                    }
                }
            }
            else
            {
                for (var i = 0; i < Constants.TonicCount; i++)
                {
                    for (var j = 0; j < Constants.ScaleCount; j++)
                    {
                        song.TonalityProbabilities[i, j] = 1.0 / (Constants.TonicCount * Constants.ScaleCount);
                    }
                }
            }

            song.Score = (1.0, 1.0);
        }

        foreach (var loop in emModel.Loops)
        {
            for (var i = 0; i < Constants.TonicCount; i++)
            {
                for (var j = 0; j < Constants.ScaleCount; j++)
                {
                    loop.TonalityProbabilities[i, j] = 1.0 / (Constants.TonicCount * Constants.ScaleCount);
                }
            }

            loop.Score = (1.0, 1.0);
        }
    }

    public double[,] CalculateProbabilities(EmContext emContext, string id, bool isSong)
    {
        var newProbabilities = new double[Constants.TonicCount, Constants.ScaleCount];
        var relevantLinks = isSong ? emContext.LoopLinksBySongId[id] : emContext.LoopLinksByLoopId[id];

        foreach (var link in relevantLinks)
        {
            var sourceProbabilities = isSong ? link.Loop.TonalityProbabilities : link.Song.TonalityProbabilities;
            var sourceScore = isSong ? link.Loop.Score : link.Song.Score;

            var songCount = emContext.SongCounts[link.Loop.Id];

            for (var i = 0; i < Constants.TonicCount; i++)
            {
                for (var j = 0; j < Constants.ScaleCount; j++)
                {
                    var targetTonic = (link.Shift - i + Constants.TonicCount) % Constants.TonicCount;
                    newProbabilities[targetTonic, j] += sourceProbabilities[i, j] * sourceScore.TonicScore * link.Weight * (1 + Math.Log(songCount));
                }
            }
        }

        NormalizeProbabilities(newProbabilities);
        return newProbabilities;
    }

    private (double TonicScore, double ScaleScore) CalculateEntropy(EmContext emContext, string id, bool isSong)
    {
        var relevantLinks = isSong ? emContext.LoopLinksBySongId[id] : emContext.LoopLinksByLoopId[id];
        var shiftCounts = relevantLinks
            .GroupBy(l => GetRelativeShift(l, isSong))
            .Select(g => g.Sum(l => l.Weight))
            .ToList();

        double totalLinks = shiftCounts.Sum();

        var tonicEntropy = shiftCounts.Select(x => x / totalLinks * Math.Log(x / totalLinks)).Sum() * -1;

        var scaleProbabilities = new double[Constants.TonicCount, Constants.ScaleCount];
        foreach (var link in relevantLinks)
        {
            var sourceProbabilities = isSong ? link.Loop.TonalityProbabilities : link.Song.TonalityProbabilities;
            for (var i = 0; i < Constants.TonicCount; i++)
            {
                for (var j = 0; j < Constants.ScaleCount; j++)
                {
                    var targetTonic = (link.Shift - i + Constants.TonicCount) % Constants.TonicCount;
                    scaleProbabilities[targetTonic, j] += sourceProbabilities[i, j];
                }
            }
        }

        var totalScaleLinks = scaleProbabilities.Cast<double>().Sum();
        var scaleEntropy = scaleProbabilities.Cast<double>().Where(p => p > 0).Select(p => p / totalScaleLinks * Math.Log(p / totalScaleLinks)).Sum() * -1;

        return (Math.Exp(-tonicEntropy), Math.Exp(-scaleEntropy));
    }

    private int GetRelativeShift(ILoopLink loopLink, bool isSong)
    {
        var predictedTonality = GetPredictedTonality(isSong 
            ? loopLink.Loop.TonalityProbabilities 
            : loopLink.Song.TonalityProbabilities);

        var targetTonic = Constants.GetMajorTonic(predictedTonality);

        return (loopLink.Shift - targetTonic + Constants.TonicCount) % Constants.TonicCount;
    }

    private double CalculateMaxChange(double[,] oldProbabilities, double[,] newProbabilities)
    {
        var maxChange = 0.0;
        for (var i = 0; i < Constants.TonicCount; i++)
        {
            for (var j = 0; j < Constants.ScaleCount; j++)
            {
                maxChange = Math.Max(maxChange, Math.Abs(oldProbabilities[i, j] - newProbabilities[i, j]));
            }
        }
        return maxChange;
    }

    private void NormalizeProbabilities(double[,] probabilities)
    {
        double sum = 0;
        for (var i = 0; i < Constants.TonicCount; i++)
        {
            for (var j = 0; j < Constants.ScaleCount; j++)
            {
                sum += probabilities[i, j];
            }
        }
        if (sum == 0) return;

        for (var i = 0; i < Constants.TonicCount; i++)
        {
            for (var j = 0; j < Constants.ScaleCount; j++)
            {
                probabilities[i, j] /= sum;
            }
        }
    }

    public (int Tonic, Scale Scale) GetPredictedTonality(double[,] probabilities)
    {
        var maxProbability = double.MinValue;
        var maxIndices = new List<(int, Scale)>();
        for (var i = 0; i < Constants.TonicCount; i++)
        {
            for (var j = 0; j < Constants.ScaleCount; j++)
            {
                if (probabilities[i, j] > maxProbability)
                {
                    maxProbability = probabilities[i, j];
                    maxIndices.Clear();
                    maxIndices.Add((i, (Scale)j));
                }
                else if (probabilities[i, j] == maxProbability)
                {
                    maxIndices.Add((i, (Scale)j));
                }
            }
        }
        var random = new Random();
        return maxIndices[random.Next(maxIndices.Count)];
    }
}