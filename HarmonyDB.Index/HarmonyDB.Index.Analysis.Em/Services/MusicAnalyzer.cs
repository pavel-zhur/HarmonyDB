using HarmonyDB.Index.Analysis.Em.Models;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.Analysis.Em.Services;

public class MusicAnalyzer(ILogger<MusicAnalyzer> logger)
{
    public EmContext CreateContext(IReadOnlyList<ILoopLink> loopLinks)
    {
        var loopLinksByLoopId = loopLinks.ToLookup(x => x.LoopId);
        return new()
        {
            LoopLinksByLoopId = loopLinksByLoopId,
            LoopLinksBySongId = loopLinks.ToLookup(x => x.SongId),
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
            var started = DateTime.Now;
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
                if (!song.KnownTonality.HasValue)
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

            logger.LogInformation($"Iteration {iterationCount}, Max Change: {maxChange:F6}, took {(DateTime.Now - started).TotalMilliseconds:N0} milliseconds");
        }

        logger.LogInformation("Converged after " + iterationCount + " iterations.");

        Parallel.ForEach(emModel.Songs, song =>
        {
            if (song.KnownTonality.HasValue)
            {
                song.TonalityProbabilities = CalculateProbabilities(emContext, song.Id, true);
            }
        });
    }

    private void InitializeProbabilities(IEmModel emModel)
    {
        foreach (var song in emModel.Songs)
        {
            if (song.KnownTonality.HasValue)
            {
                for (byte i = 0; i < Constants.TonicCount; i++)
                {
                    for (byte j = 0; j < Constants.ScaleCount; j++)
                    {
                        song.TonalityProbabilities[i, j] = i == song.KnownTonality.Value.Tonic && j == (byte)song.KnownTonality.Value.Scale ? 1.0f : 0.0f;
                    }
                }
            }
            else
            {
                for (byte i = 0; i < Constants.TonicCount; i++)
                {
                    for (byte j = 0; j < Constants.ScaleCount; j++)
                    {
                        song.TonalityProbabilities[i, j] = 1.0f / (Constants.TonicCount * Constants.ScaleCount);
                    }
                }
            }

            song.Score = (1.0f, 1.0f);
        }

        foreach (var loop in emModel.Loops)
        {
            for (byte i = 0; i < Constants.TonicCount; i++)
            {
                for (byte j = 0; j < Constants.ScaleCount; j++)
                {
                    loop.TonalityProbabilities[i, j] = 1.0f / (Constants.TonicCount * Constants.ScaleCount);
                }
            }

            loop.Score = (1.0f, 1.0f);
        }
    }

    public float[,] CalculateProbabilities(EmContext emContext, string id, bool isSong)
    {
        var newProbabilities = new float[Constants.TonicCount, Constants.ScaleCount];
        var relevantLinks = isSong ? emContext.LoopLinksBySongId[id] : emContext.LoopLinksByLoopId[id];

        foreach (var link in relevantLinks)
        {
            var sourceProbabilities = isSong ? link.Loop.TonalityProbabilities : link.Song.TonalityProbabilities;
            var sourceScore = isSong ? link.Loop.Score : link.Song.Score;

            var songCount = emContext.SongCounts[link.Loop.Id];

            for (byte i = 0; i < Constants.TonicCount; i++)
            {
                for (byte j = 0; j < Constants.ScaleCount; j++)
                {
                    var targetTonic = (link.Shift - i + Constants.TonicCount) % Constants.TonicCount;
                    newProbabilities[targetTonic, j] += (sourceProbabilities[i, j] * sourceScore.TonicScore * link.Weight * (1 + (float)Math.Log(songCount)));
                }
            }
        }

        NormalizeProbabilities(newProbabilities);
        return newProbabilities;
    }

    private (float TonicScore, float ScaleScore) CalculateEntropy(EmContext emContext, string id, bool isSong)
    {
        var relevantLinks = isSong ? emContext.LoopLinksBySongId[id] : emContext.LoopLinksByLoopId[id];

        var shiftCounts = new float[Constants.TonicCount];
        foreach (var relevantLink in relevantLinks)
        {
            AddShiftProbabilities(shiftCounts, relevantLink, isSong);
        }

        var totalLinks = shiftCounts.Sum();

        var tonicEntropy = shiftCounts.Where(x => x > 0).Select(x => x / totalLinks * (float)Math.Log(x / totalLinks)).Sum() * -1;

        var scaleProbabilities = new float[Constants.TonicCount, Constants.ScaleCount];
        foreach (var link in relevantLinks)
        {
            var sourceProbabilities = isSong ? link.Loop.TonalityProbabilities : link.Song.TonalityProbabilities;
            for (byte i = 0; i < Constants.TonicCount; i++)
            {
                for (byte j = 0; j < Constants.ScaleCount; j++)
                {
                    var targetTonic = (link.Shift - i + Constants.TonicCount) % Constants.TonicCount;
                    scaleProbabilities[targetTonic, j] += sourceProbabilities[i, j];
                }
            }
        }

        var totalScaleLinks = scaleProbabilities.Cast<float>().Sum();
        var scaleEntropy = scaleProbabilities.Cast<float>().Where(p => p > 0).Select(p => p / totalScaleLinks * Math.Log(p / totalScaleLinks)).Sum() * -1;

        return ((float)Math.Exp(-tonicEntropy), (float)Math.Exp(-scaleEntropy));
    }

    private void AddShiftProbabilities(float[] shiftsCounts, ILoopLink loopLink, bool isSong)
    {
        var probabilities = isSong
            ? loopLink.Loop.TonalityProbabilities
            : loopLink.Song.TonalityProbabilities;

        foreach (var (tonic, scale) in Constants.Indices)
        {
            var majorTonic = Constants.GetMajorTonic((tonic, (Scale)scale));
            var relativeShift = (loopLink.Shift - majorTonic + Constants.TonicCount) % Constants.TonicCount;
            shiftsCounts[relativeShift] += probabilities[tonic, scale] * loopLink.Weight;
        }
    }

    private float CalculateMaxChange(float[,] oldProbabilities, float[,] newProbabilities)
    {
        var maxChange = 0.0f;
        for (byte i = 0; i < Constants.TonicCount; i++)
        {
            for (byte j = 0; j < Constants.ScaleCount; j++)
            {
                maxChange = Math.Max(maxChange, Math.Abs(oldProbabilities[i, j] - newProbabilities[i, j]));
            }
        }
        return maxChange;
    }

    private void NormalizeProbabilities(float[,] probabilities)
    {
        float sum = 0;
        for (byte i = 0; i < Constants.TonicCount; i++)
        {
            for (byte j = 0; j < Constants.ScaleCount; j++)
            {
                sum += probabilities[i, j];
            }
        }
        if (sum == 0) return;

        for (byte i = 0; i < Constants.TonicCount; i++)
        {
            for (byte j = 0; j < Constants.ScaleCount; j++)
            {
                probabilities[i, j] /= sum;
            }
        }
    }

    public (byte Tonic, Scale Scale) GetPredictedTonality(float[,] probabilities)
    {
        var maxProbability = float.MinValue;
        var maxIndices = new List<(byte, Scale)>();
        for (byte i = 0; i < Constants.TonicCount; i++)
        {
            for (byte j = 0; j < Constants.ScaleCount; j++)
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