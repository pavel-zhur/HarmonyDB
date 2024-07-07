using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.Analysis.Em.Services;

public class MusicAnalyzer(ILogger<MusicAnalyzer> logger)
{
    private bool isInitialized;
    private Dictionary<string, ISong> Songs;
    private Dictionary<string, ILoop> Loops;
    private List<LoopLink> LoopLinks;
    
    public void Initialize(Dictionary<string, ISong> songs, Dictionary<string, ILoop> loops, List<LoopLink> loopLinks)
    {
        if (isInitialized) throw new InvalidOperationException("Already initialized");

        Songs = songs;
        Loops = loops;
        LoopLinks = loopLinks;
        InitializeProbabilities();
    }

    private void InitializeProbabilities()
    {
        foreach (var song in Songs.Values)
        {
            if (song.IsTonalityKnown)
            {
                for (int i = 0; i < Constants.TonicCount; i++)
                {
                    for (int j = 0; j < Constants.ScaleCount; j++)
                    {
                        song.TonalityProbabilities[i, j] = i == song.KnownTonality.Item1 && j == (int)song.KnownTonality.Item2 ? 1.0 : 0.0;
                    }
                }
            }
            else
            {
                for (int i = 0; i < Constants.TonicCount; i++)
                {
                    for (int j = 0; j < Constants.ScaleCount; j++)
                    {
                        song.TonalityProbabilities[i, j] = 1.0 / (Constants.TonicCount * Constants.ScaleCount);
                    }
                }
            }
        }

        foreach (var loop in Loops.Values)
        {
            for (int i = 0; i < Constants.TonicCount; i++)
            {
                for (int j = 0; j < Constants.ScaleCount; j++)
                {
                    loop.TonalityProbabilities[i, j] = 1.0 / (Constants.TonicCount * Constants.ScaleCount);
                }
            }
        }

        // Calculate the number of distinct songs for each loop
        foreach (var loop in Loops.Values)
        {
            loop.SongCount = LoopLinks.Where(l => l.LoopId == loop.Id).Select(l => l.SongId).Distinct().Count();
        }
    }

    public void UpdateProbabilities()
    {
        const double tolerance = 0.01;
        const int stabilityCheckWindow = 5; // Number of iterations to check for stability
        const double stabilityThreshold = 1e-6; // Threshold for considering changes as stable
        List<double> recentMaxChanges = new List<double>();
        bool hasConverged = false;
        int iterationCount = 0;

        while (!hasConverged)
        {
            iterationCount++;
            double maxChange = 0.0;

            Parallel.ForEach(Songs.Values, song =>
            {
                if (!song.IsTonalityKnown)
                {
                    var newProbabilities = CalculateProbabilities(song.Id, true);
                    var change = CalculateMaxChange(song.TonalityProbabilities, newProbabilities);
                    lock (song)
                    {
                        maxChange = Math.Max(maxChange, change);
                        song.TonalityProbabilities = newProbabilities;
                    }
                }
                song.Score = CalculateEntropy(song.Id, true);
            });

            Parallel.ForEach(Loops.Values, loop =>
            {
                var newProbabilities = CalculateProbabilities(loop.Id, false);
                var change = CalculateMaxChange(loop.TonalityProbabilities, newProbabilities);
                lock (loop)
                {
                    maxChange = Math.Max(maxChange, change);
                    loop.TonalityProbabilities = newProbabilities;
                }
                loop.Score = CalculateEntropy(loop.Id, false);
            });

            recentMaxChanges.Add(maxChange);
            if (recentMaxChanges.Count > stabilityCheckWindow)
            {
                recentMaxChanges.RemoveAt(0);
            }

            if (recentMaxChanges.Count == stabilityCheckWindow)
            {
                double minChange = recentMaxChanges.Min();
                double maxInWindow = recentMaxChanges.Max();

                if (maxInWindow - minChange < stabilityThreshold)
                {
                    throw new Exception("Algorithm is oscillating without converging.");
                }
            }

            if (maxChange < tolerance)
            {
                hasConverged = true;
            }

            logger.LogInformation($"Iteration {iterationCount}, Max Change: {maxChange:F6}");
        }

        logger.LogInformation("Converged after " + iterationCount + " iterations.");
    }

    public double[,] CalculateProbabilities(string id, bool isSong)
    {
        double[,] newProbabilities = new double[Constants.TonicCount, Constants.ScaleCount];
        IEnumerable<LoopLink> relevantLinks = isSong ? LoopLinks.Where(l => l.SongId == id) : LoopLinks.Where(l => l.LoopId == id);

        foreach (var link in relevantLinks)
        {
            int adjustedTonic = (link.Shift + Constants.TonicCount) % Constants.TonicCount;
            double[,] sourceProbabilities = isSong ? Loops[link.LoopId].TonalityProbabilities : Songs[link.SongId].TonalityProbabilities;
            (double TonicScore, double ScaleScore) sourceScore = isSong ? Loops[link.LoopId].Score : Songs[link.SongId].Score;

            for (int i = 0; i < Constants.TonicCount; i++)
            {
                for (int j = 0; j < Constants.ScaleCount; j++)
                {
                    int targetTonic = isSong ? (i - adjustedTonic + Constants.TonicCount) % Constants.TonicCount : (adjustedTonic + i) % Constants.TonicCount;
                    newProbabilities[targetTonic, j] += sourceProbabilities[i, j] * sourceScore.TonicScore * link.Count;
                }
            }
        }

        NormalizeProbabilities(newProbabilities);
        return newProbabilities;
    }

    private (double TonicScore, double ScaleScore) CalculateEntropy(string id, bool isSong)
    {
        IEnumerable<LoopLink> relevantLinks = isSong ? LoopLinks.Where(l => l.SongId == id) : LoopLinks.Where(l => l.LoopId == id);
        var shiftCounts = relevantLinks.GroupBy(l => GetRelativeShift(l, isSong)).ToDictionary(g => g.Key, g => g.Sum(l => l.Count));
        double totalLinks = relevantLinks.Sum(l => l.Count);

        double tonicEntropy = shiftCounts.Values.Select(count => count / totalLinks * Math.Log(count / totalLinks)).Sum() * -1;

        double[,] scaleProbabilities = new double[Constants.TonicCount, Constants.ScaleCount];
        foreach (var link in relevantLinks)
        {
            int adjustedTonic = (link.Shift + Constants.TonicCount) % Constants.TonicCount;
            double[,] sourceProbabilities = isSong ? Loops[link.LoopId].TonalityProbabilities : Songs[link.SongId].TonalityProbabilities;
            for (int i = 0; i < Constants.TonicCount; i++)
            {
                for (int j = 0; j < Constants.ScaleCount; j++)
                {
                    int targetTonic = isSong ? (i - adjustedTonic + Constants.TonicCount) % Constants.TonicCount : (adjustedTonic + i) % Constants.TonicCount;
                    scaleProbabilities[targetTonic, j] += sourceProbabilities[i, j];
                }
            }
        }

        double totalScaleLinks = scaleProbabilities.Cast<double>().Sum();
        double scaleEntropy = scaleProbabilities.Cast<double>().Where(p => p > 0).Select(p => p / totalScaleLinks * Math.Log(p / totalScaleLinks)).Sum() * -1;

        return (Math.Exp(-tonicEntropy), Math.Exp(-scaleEntropy));
    }

    private int GetRelativeShift(LoopLink loopLink, bool isSong)
    {
        var shift = loopLink.Shift;

        if (isSong)
        {
            int loopTonic = GetPredictedTonality(Loops[loopLink.LoopId].TonalityProbabilities).Item1;
            return (loopTonic - shift + Constants.TonicCount) % Constants.TonicCount;
        }
        else
        {
            int songTonic = GetPredictedTonality(Songs[loopLink.SongId].TonalityProbabilities).Item1;
            return (songTonic + shift) % Constants.TonicCount;
        }
    }

    private double CalculateMaxChange(double[,] oldProbabilities, double[,] newProbabilities)
    {
        double maxChange = 0.0;
        for (int i = 0; i < Constants.TonicCount; i++)
        {
            for (int j = 0; j < Constants.ScaleCount; j++)
            {
                maxChange = Math.Max(maxChange, Math.Abs(oldProbabilities[i, j] - newProbabilities[i, j]));
            }
        }
        return maxChange;
    }

    private void NormalizeProbabilities(double[,] probabilities)
    {
        double sum = 0;
        for (int i = 0; i < Constants.TonicCount; i++)
        {
            for (int j = 0; j < Constants.ScaleCount; j++)
            {
                sum += probabilities[i, j];
            }
        }
        if (sum == 0) return;

        for (int i = 0; i < Constants.TonicCount; i++)
        {
            for (int j = 0; j < Constants.ScaleCount; j++)
            {
                probabilities[i, j] /= sum;
            }
        }
    }

    public (int, Scale) GetPredictedTonality(double[,] probabilities)
    {
        double maxProbability = double.MinValue;
        var maxIndices = new List<(int, Scale)>();
        for (int i = 0; i < Constants.TonicCount; i++)
        {
            for (int j = 0; j < Constants.ScaleCount; j++)
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
        Random random = new Random();
        return maxIndices[random.Next(maxIndices.Count)];
    }
}