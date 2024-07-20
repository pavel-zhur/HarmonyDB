public class MusicAnalyzer
{
    private readonly Dictionary<string, ISong> _songs;
    private readonly Dictionary<string, ILoop> _loops;
    private readonly List<LoopLink> _loopLinks;

    public MusicAnalyzer(Dictionary<string, ISong> songs, Dictionary<string, ILoop> loops, List<LoopLink> loopLinks)
    {
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
                for (var i = 0; i < Constants.TonicCount; i++)
                {
                    for (var j = 0; j < Constants.ScaleCount; j++)
                    {
                        song.TonalityProbabilities[i, j] = (i == song.KnownTonality.Item1 && j == (int)song.KnownTonality.Item2) ? 1.0 : 0.0;
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
        }

        foreach (var loop in _loops.Values)
        {
            for (var i = 0; i < Constants.TonicCount; i++)
            {
                for (var j = 0; j < Constants.ScaleCount; j++)
                {
                    loop.TonalityProbabilities[i, j] = 1.0 / (Constants.TonicCount * Constants.ScaleCount);
                }
            }
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

            Console.WriteLine($"Iteration {iterationCount}, Max Change: {maxChange:F6}");
        }

        Console.WriteLine("Converged after " + iterationCount + " iterations.");
    }

    public double[,] CalculateProbabilities(string id, bool isSong)
    {
        var newProbabilities = new double[Constants.TonicCount, Constants.ScaleCount];
        var relevantLinks = isSong ? _loopLinks.Where(l => l.SongId == id) : _loopLinks.Where(l => l.LoopId == id);

        foreach (var link in relevantLinks)
        {
            var adjustedTonic = (link.Shift + Constants.TonicCount) % Constants.TonicCount;
            var sourceProbabilities = isSong ? _loops[link.LoopId].TonalityProbabilities : _songs[link.SongId].TonalityProbabilities;
            var sourceScore = isSong ? _loops[link.LoopId].Score : _songs[link.SongId].Score;

            for (var i = 0; i < Constants.TonicCount; i++)
            {
                for (var j = 0; j < Constants.ScaleCount; j++)
                {
                    var targetTonic = isSong ? (i - adjustedTonic + Constants.TonicCount) % Constants.TonicCount : (adjustedTonic + i) % Constants.TonicCount;
                    newProbabilities[targetTonic, j] += sourceProbabilities[i, j] * sourceScore.TonicScore * link.Count;
                }
            }
        }

        NormalizeProbabilities(newProbabilities);
        return newProbabilities;
    }

    private (double TonicScore, double ScaleScore) CalculateEntropy(string id, bool isSong)
    {
        var relevantLinks = isSong ? _loopLinks.Where(l => l.SongId == id) : _loopLinks.Where(l => l.LoopId == id);
        var shiftCounts = relevantLinks.GroupBy(l => GetRelativeShift(l, isSong)).ToDictionary(g => g.Key, g => g.Sum(l => l.Count));
        double totalLinks = relevantLinks.Sum(l => l.Count);

        var tonicEntropy = shiftCounts.Values.Select(count => (count / totalLinks) * Math.Log(count / totalLinks)).Sum() * -1;

        var scaleProbabilities = new double[Constants.TonicCount, Constants.ScaleCount];
        foreach (var link in relevantLinks)
        {
            var adjustedTonic = (link.Shift + Constants.TonicCount) % Constants.TonicCount;
            var sourceProbabilities = isSong ? _loops[link.LoopId].TonalityProbabilities : _songs[link.SongId].TonalityProbabilities;
            for (var i = 0; i < Constants.TonicCount; i++)
            {
                for (var j = 0; j < Constants.ScaleCount; j++)
                {
                    var targetTonic = isSong ? (i - adjustedTonic + Constants.TonicCount) % Constants.TonicCount : (adjustedTonic + i) % Constants.TonicCount;
                    scaleProbabilities[targetTonic, j] += sourceProbabilities[i, j];
                }
            }
        }

        var totalScaleLinks = scaleProbabilities.Cast<double>().Sum();
        var scaleEntropy = scaleProbabilities.Cast<double>().Where(p => p > 0).Select(p => (p / totalScaleLinks) * Math.Log(p / totalScaleLinks)).Sum() * -1;

        return (Math.Exp(-tonicEntropy), Math.Exp(-scaleEntropy));
    }

    private int GetRelativeShift(LoopLink loopLink, bool isSong)
    {
        var shift = loopLink.Shift;

        if (isSong)
        {
            var loopTonic = GetPredictedTonality(_loops[loopLink.LoopId].TonalityProbabilities).Item1;
            return (loopTonic - shift + Constants.TonicCount) % Constants.TonicCount;
        }
        else
        {
            var songTonic = GetPredictedTonality(_songs[loopLink.SongId].TonalityProbabilities).Item1;
            return (songTonic + shift) % Constants.TonicCount;
        }
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

    public static (int, Scale) GetPredictedTonality(double[,] probabilities)
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
