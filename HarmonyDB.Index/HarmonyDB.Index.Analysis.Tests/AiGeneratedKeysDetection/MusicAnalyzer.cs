using Xunit.Abstractions;

namespace HarmonyDB.Index.Analysis.Tests.AiGeneratedKeysDetection;

public class MusicAnalyzer
{
    private readonly ITestOutputHelper _logger;
    private readonly Dictionary<string, ISong> _songs;
    private readonly Dictionary<string, ILoop> _loops;
    private readonly List<LoopLink> _loopLinks;

    public MusicAnalyzer(ITestOutputHelper logger, Dictionary<string, ISong> songs, Dictionary<string, ILoop> loops, List<LoopLink> loopLinks)
    {
        _logger = logger;
        _songs = songs;
        _loops = loops;
        _loopLinks = loopLinks;
        InitializeProbabilities();
    }

    private void InitializeProbabilities()
    {
        foreach (var song in _songs.Values)
        {
            if (song.IsTonalityKnown)
            {
                for (var i = 0; i < Constants.TonalityCount; i++)
                    song.TonalityProbabilities[i] = i == song.KnownTonality ? 1.0 : 0.0;
            }
            else
            {
                for (var i = 0; i < Constants.TonalityCount; i++)
                    song.TonalityProbabilities[i] = 1.0 / Constants.TonalityCount;
            }
        }

        foreach (var loop in _loops.Values)
        {
            for (var i = 0; i < Constants.TonalityCount; i++)
                loop.TonalityProbabilities[i] = 1.0 / Constants.TonalityCount;
        }

        // Calculate the number of distinct songs for each loop
        foreach (var loop in _loops.Values)
        {
            loop.SongCount = _loopLinks.Where(l => l.LoopId == loop.Id).Select(l => l.SongId).Distinct().Count();
        }
    }

    public void UpdateProbabilities()
    {
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

            Parallel.ForEach(_songs.Values, song =>
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

            Parallel.ForEach(_loops.Values, loop =>
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

            _logger.WriteLine($"Iteration {iterationCount}, Max Change: {maxChange:F6}");
        }

        _logger.WriteLine("Converged after " + iterationCount + " iterations.");
    }

    public double[] CalculateProbabilities(string id, bool isSong)
    {
        var newProbabilities = new double[Constants.TonalityCount];
        IEnumerable<LoopLink> relevantLinks = isSong ? _loopLinks.Where(l => l.SongId == id) : _loopLinks.Where(l => l.LoopId == id);

        foreach (var link in relevantLinks)
        {
            var adjustedTonality = (link.Shift + Constants.TonalityCount) % Constants.TonalityCount;
            var sourceProbabilities = isSong ? _loops[link.LoopId].TonalityProbabilities : _songs[link.SongId].TonalityProbabilities;
            var sourceScore = isSong ? _loops[link.LoopId].Score * _loops[link.LoopId].SongCount : _songs[link.SongId].Score;

            for (var i = 0; i < Constants.TonalityCount; i++)
            {
                var targetTonality = isSong ? (i - adjustedTonality + Constants.TonalityCount) % Constants.TonalityCount : (adjustedTonality + i) % Constants.TonalityCount;
                newProbabilities[targetTonality] += sourceProbabilities[i] * sourceScore * link.Count;
            }
        }

        NormalizeProbabilities(newProbabilities);
        return newProbabilities;
    }

    private double CalculateEntropy(string id, bool isSong)
    {
        IEnumerable<LoopLink> relevantLinks = isSong ? _loopLinks.Where(l => l.SongId == id) : _loopLinks.Where(l => l.LoopId == id);
        var shiftCounts = relevantLinks.GroupBy(l => GetRelativeShift(l, isSong)).ToDictionary(g => g.Key, g => g.Sum(l => l.Count));
        double totalLinks = relevantLinks.Sum(l => l.Count);
        var entropy = shiftCounts.Values.Select(count => count / totalLinks * Math.Log(count / totalLinks)).Sum() * -1;
        return Math.Exp(-entropy);
    }

    private int GetRelativeShift(LoopLink loopLink, bool isSong)
    {
        var shift = loopLink.Shift;

        if (isSong)
        {
            var loopTonality = GetPredictedTonality(_loops[loopLink.LoopId].TonalityProbabilities);
            return (loopTonality - shift + Constants.TonalityCount) % Constants.TonalityCount;
        }
        else
        {
            var songTonality = GetPredictedTonality(_songs[loopLink.SongId].TonalityProbabilities);
            return (songTonality + shift) % Constants.TonalityCount;
        }
    }

    private double CalculateMaxChange(double[] oldProbabilities, double[] newProbabilities)
    {
        var maxChange = 0.0;
        for (var i = 0; i < Constants.TonalityCount; i++)
        {
            maxChange = Math.Max(maxChange, Math.Abs(oldProbabilities[i] - newProbabilities[i]));
        }
        return maxChange;
    }

    private void NormalizeProbabilities(double[] probabilities)
    {
        var sum = probabilities.Sum();
        if (sum == 0) return;

        for (var i = 0; i < Constants.TonalityCount; i++)
        {
            probabilities[i] /= sum;
        }
    }

    public static int GetPredictedTonality(double[] probabilities)
    {
        var maxProbability = probabilities.Max();
        var maxIndices = probabilities.Select((value, index) => new { value, index })
            .Where(x => x.value == maxProbability)
            .Select(x => x.index)
            .ToArray();
        var random = new Random();
        return maxIndices[random.Next(maxIndices.Length)];
    }
}