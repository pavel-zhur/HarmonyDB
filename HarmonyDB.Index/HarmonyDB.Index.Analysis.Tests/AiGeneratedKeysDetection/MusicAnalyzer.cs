using Xunit.Abstractions;

namespace HarmonyDB.Index.Analysis.Tests.AiGeneratedKeysDetection;

public class MusicAnalyzer
{
    private readonly ITestOutputHelper _logger;
    private Dictionary<string, ISong> Songs;
    private Dictionary<string, ILoop> Loops;
    private List<LoopLink> LoopLinks;

    public MusicAnalyzer(ITestOutputHelper logger, Dictionary<string, ISong> songs, Dictionary<string, ILoop> loops, List<LoopLink> loopLinks)
    {
        _logger = logger;
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
                for (int i = 0; i < Constants.TonalityCount; i++)
                    song.TonalityProbabilities[i] = i == song.KnownTonality ? 1.0 : 0.0;
            }
            else
            {
                for (int i = 0; i < Constants.TonalityCount; i++)
                    song.TonalityProbabilities[i] = 1.0 / Constants.TonalityCount;
            }
        }

        foreach (var loop in Loops.Values)
        {
            for (int i = 0; i < Constants.TonalityCount; i++)
                loop.TonalityProbabilities[i] = 1.0 / Constants.TonalityCount;
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

            _logger.WriteLine($"Iteration {iterationCount}, Max Change: {maxChange:F6}");
        }

        _logger.WriteLine("Converged after " + iterationCount + " iterations.");
    }

    public double[] CalculateProbabilities(string id, bool isSong)
    {
        double[] newProbabilities = new double[Constants.TonalityCount];
        IEnumerable<LoopLink> relevantLinks = isSong ? LoopLinks.Where(l => l.SongId == id) : LoopLinks.Where(l => l.LoopId == id);

        foreach (var link in relevantLinks)
        {
            int adjustedTonality = (link.Shift + Constants.TonalityCount) % Constants.TonalityCount;
            double[] sourceProbabilities = isSong ? Loops[link.LoopId].TonalityProbabilities : Songs[link.SongId].TonalityProbabilities;
            double sourceScore = isSong ? Loops[link.LoopId].Score * Loops[link.LoopId].SongCount : Songs[link.SongId].Score;

            for (int i = 0; i < Constants.TonalityCount; i++)
            {
                int targetTonality = isSong ? (i - adjustedTonality + Constants.TonalityCount) % Constants.TonalityCount : (adjustedTonality + i) % Constants.TonalityCount;
                newProbabilities[targetTonality] += sourceProbabilities[i] * sourceScore * link.Count;
            }
        }

        NormalizeProbabilities(newProbabilities);
        return newProbabilities;
    }

    private double CalculateEntropy(string id, bool isSong)
    {
        IEnumerable<LoopLink> relevantLinks = isSong ? LoopLinks.Where(l => l.SongId == id) : LoopLinks.Where(l => l.LoopId == id);
        var shiftCounts = relevantLinks.GroupBy(l => GetRelativeShift(l, isSong)).ToDictionary(g => g.Key, g => g.Sum(l => l.Count));
        double totalLinks = relevantLinks.Sum(l => l.Count);
        double entropy = shiftCounts.Values.Select(count => count / totalLinks * Math.Log(count / totalLinks)).Sum() * -1;
        return Math.Exp(-entropy);
    }

    private int GetRelativeShift(LoopLink loopLink, bool isSong)
    {
        var shift = loopLink.Shift;

        if (isSong)
        {
            int loopTonality = GetPredictedTonality(Loops[loopLink.LoopId].TonalityProbabilities);
            return (loopTonality - shift + Constants.TonalityCount) % Constants.TonalityCount;
        }
        else
        {
            int songTonality = GetPredictedTonality(Songs[loopLink.SongId].TonalityProbabilities);
            return (songTonality + shift) % Constants.TonalityCount;
        }
    }

    private double CalculateMaxChange(double[] oldProbabilities, double[] newProbabilities)
    {
        double maxChange = 0.0;
        for (int i = 0; i < Constants.TonalityCount; i++)
        {
            maxChange = Math.Max(maxChange, Math.Abs(oldProbabilities[i] - newProbabilities[i]));
        }
        return maxChange;
    }

    private void NormalizeProbabilities(double[] probabilities)
    {
        double sum = probabilities.Sum();
        if (sum == 0) return;

        for (int i = 0; i < Constants.TonalityCount; i++)
        {
            probabilities[i] /= sum;
        }
    }

    public static int GetPredictedTonality(double[] probabilities)
    {
        double maxProbability = probabilities.Max();
        var maxIndices = probabilities.Select((value, index) => new { value, index })
            .Where(x => x.value == maxProbability)
            .Select(x => x.index)
            .ToArray();
        Random random = new Random();
        return maxIndices[random.Next(maxIndices.Length)];
    }
}