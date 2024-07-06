using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public interface ISource
{
    string Id { get; set; }
    double[] TonalityProbabilities { get; set; }
    double Score { get; set; }
}

public interface ISong : ISource
{
    bool IsTonalityKnown { get; set; }
    int KnownTonality { get; set; }
}

public interface ILoop : ISource
{
    int SongCount { get; set; }
}

public class Song : ISong
{
    public string Id { get; set; }
    public double[] TonalityProbabilities { get; set; } = new double[Constants.TonalityCount];
    public double Score { get; set; } = 1.0;
    public bool IsTonalityKnown { get; set; }
    public int KnownTonality { get; set; }
    public int[] SecretTonalities { get; set; }
    public bool IsKnownTonalityIncorrect { get; set; }
}

public class Loop : ILoop
{
    public string Id { get; set; }
    public double[] TonalityProbabilities { get; set; } = new double[Constants.TonalityCount];
    public double Score { get; set; } = 1.0;
    public int SongCount { get; set; } = 0;
    public int[] SecretTonalities { get; set; }
}

public class LoopLink
{
    public string SongId { get; set; }
    public string LoopId { get; set; }
    public int Shift { get; set; }
    public int Count { get; set; }
}

public static class Constants
{
    public const int TonalityCount = 12;
}

public class MusicAnalyzer
{
    private Dictionary<string, ISong> Songs;
    private Dictionary<string, ILoop> Loops;
    private List<LoopLink> LoopLinks;

    public MusicAnalyzer(Dictionary<string, ISong> songs, Dictionary<string, ILoop> loops, List<LoopLink> loopLinks)
    {
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
                    song.TonalityProbabilities[i] = (i == song.KnownTonality) ? 1.0 : 0.0;
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

        // Подсчет количества различных песен для каждого цикла
        foreach (var loop in Loops.Values)
        {
            loop.SongCount = LoopLinks.Where(l => l.LoopId == loop.Id).Select(l => l.SongId).Distinct().Count();
        }
    }

    public void UpdateProbabilities()
    {
        const double tolerance = 0.01;
        bool hasConverged = false;
        int iterationCount = 0;

        while (!hasConverged)
        {
            hasConverged = true;
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

            if (maxChange > tolerance)
                hasConverged = false;

            Console.WriteLine($"Iteration {iterationCount}, Max Change: {maxChange:F6}");
        }
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
        var shiftCounts = relevantLinks.GroupBy(l => l.Shift).ToDictionary(g => g.Key, g => g.Sum(l => l.Count));
        double totalLinks = relevantLinks.Sum(l => l.Count);
        double entropy = shiftCounts.Values.Select(count => (count / totalLinks) * Math.Log(count / totalLinks)).Sum() * -1;
        return Math.Exp(-entropy);
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
}

public class TestDataGeneratorParameters
{
    public int TotalSongs { get; set; } = 1000;
    public int TotalLoops { get; set; } = 1500;
    public int MinLoopsPerSong { get; set; } = 2;
    public int MaxLoopsPerSong { get; set; } = 7;
    public double KnownTonalityProbability { get; set; } = 0.15;
    public double ModulationProbability { get; set; } = 0.10;
    public double BadCycleProbability { get; set; } = 0.10;
    public double IncorrectKnownTonalityProbability { get; set; } = 0.05;
    public int MinLoopAppearances { get; set; } = 1;
    public int MaxLoopAppearances { get; set; } = 4;
}

public class TestDataGenerator
{
    private Random random = new Random();

    public (Dictionary<string, Song>, Dictionary<string, Loop>, List<LoopLink>) GenerateTestData(TestDataGeneratorParameters parameters)
    {
        var songs = new Dictionary<string, Song>();
        var loops = new Dictionary<string, Loop>();
        var loopLinks = new List<LoopLink>();

        // Generate Loops
        for (int i = 0; i < parameters.TotalLoops; i++)
        {
            string loopId = $"loop{i + 1}";
            bool isBadLoop = random.NextDouble() < parameters.BadCycleProbability;
            int[] loopTonalities = isBadLoop ? GenerateRandomTonalities(2, 3) : new[] { random.Next(Constants.TonalityCount) };
            loops[loopId] = new Loop
            {
                Id = loopId,
                SecretTonalities = loopTonalities
            };
        }

        // Generate Songs
        for (int i = 0; i < parameters.TotalSongs; i++)
        {
            string songId = $"song{i + 1}";
            bool hasModulation = random.NextDouble() < parameters.ModulationProbability;
            int[] songTonalities = hasModulation ? GenerateRandomTonalities(2, 3) : new[] { random.Next(Constants.TonalityCount) };
            bool isKnownTonality = random.NextDouble() < parameters.KnownTonalityProbability;
            bool isKnownTonalityIncorrect = isKnownTonality && random.NextDouble() < parameters.IncorrectKnownTonalityProbability;

            int knownTonality = isKnownTonality ? songTonalities[random.Next(songTonalities.Length)] : -1;

            // If known tonality is incorrect, assign it a different tonality
            if (isKnownTonalityIncorrect)
            {
                var possibleTonalities = Enumerable.Range(0, Constants.TonalityCount).Except(songTonalities).ToArray();
                knownTonality = possibleTonalities[random.Next(possibleTonalities.Length)];
            }

            songs[songId] = new Song
            {
                Id = songId,
                IsTonalityKnown = isKnownTonality,
                KnownTonality = knownTonality,
                SecretTonalities = songTonalities,
                IsKnownTonalityIncorrect = isKnownTonalityIncorrect
            };

            int loopCount = random.Next(parameters.MinLoopsPerSong, parameters.MaxLoopsPerSong + 1);
            var songLoops = GetRandomLoops(loops.Keys.ToList(), loopCount);

            foreach (var loopId in songLoops)
            {
                var loop = loops[loopId];
                int loopTonality = GetRandomTonality(loop.SecretTonalities);
                int songTonality = songTonalities[random.Next(songTonalities.Length)];
                int shift = (loopTonality - songTonality + Constants.TonalityCount) % Constants.TonalityCount;

                loopLinks.Add(new LoopLink
                {
                    SongId = songId,
                    LoopId = loopId,
                    Shift = shift,
                    Count = random.Next(parameters.MinLoopAppearances, parameters.MaxLoopAppearances + 1) // Random count of appearances of the loop in the song
                });
            }
        }

        return (songs, loops, loopLinks);
    }

    private int[] GenerateRandomTonalities(int min, int max)
    {
        int count = random.Next(min, max + 1);
        var tonalities = new HashSet<int>();
        while (tonalities.Count < count)
        {
            tonalities.Add(random.Next(Constants.TonalityCount));
        }
        return tonalities.ToArray();
    }

    private List<string> GetRandomLoops(List<string> loopIds, int count)
    {
        var selectedLoops = new List<string>();
        while (selectedLoops.Count < count)
        {
            string loopId = loopIds[random.Next(loopIds.Count)];
            if (!selectedLoops.Contains(loopId))
            {
                selectedLoops.Add(loopId);
            }
        }
        return selectedLoops;
    }

    private int GetRandomTonality(int[] tonalities)
    {
        int index = random.Next(tonalities.Length);
        return tonalities[index];
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        var generatorParameters = new TestDataGeneratorParameters();
        var generator = new TestDataGenerator();
        var (songs, loops, loopLinks) = generator.GenerateTestData(generatorParameters);

        // Вывод исходных данных
        Console.WriteLine($"Generated {songs.Count} songs and {loops.Count} loops.");
        Console.WriteLine($"First 10 songs:");
        foreach (var song in songs.Take(10))
        {
            var s = song.Value;
            Console.WriteLine($"Song {song.Key}: Known Tonality = {s.KnownTonality}, Is Tonality Known = {s.IsTonalityKnown}, Secret Tonalities = {string.Join(",", s.SecretTonalities)}, Is Known Tonality Incorrect = {s.IsKnownTonalityIncorrect}");
        }

        Console.WriteLine($"First 10 loops:");
        foreach (var loop in loops.Take(10))
        {
            var l = loop.Value;
            Console.WriteLine($"Loop {loop.Key}: Secret Tonalities = {string.Join(",", l.SecretTonalities)}");
        }

        Console.WriteLine($"First 10 loop links:");
        foreach (var link in loopLinks.Take(10))
        {
            Console.WriteLine($"Link: Song {link.SongId}, Loop {link.LoopId}, Shift = {link.Shift}, Count = {link.Count}");
        }

        var analyzer = new MusicAnalyzer(songs.ToDictionary(kv => kv.Key, kv => (ISong)kv.Value), loops.ToDictionary(kv => kv.Key, kv => (ILoop)kv.Value), loopLinks);
        analyzer.UpdateProbabilities();

        // Примерный диапазон и средний score для плохих и хороших объектов
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
            Console.WriteLine($"{label}: Min Score = {scoresArray.Min():F4}, Max Score = {scoresArray.Max():F4}, Average Score = {scoresArray.Average():F4}, Median = {Percentile(scoresArray, 0.5):F4}, 90th Percentile = {Percentile(scoresArray, 0.9):F4}");
        }

        Console.WriteLine("\nSummary of Scores:");
        PrintStatistics(allGoodSongs.Select(s => s.Score), "Good Songs");
        PrintStatistics(allBadSongs.Select(s => s.Score), "Bad Songs");
        PrintStatistics(allGoodLoops.Select(l => l.Score), "Good Loops");
        PrintStatistics(allBadLoops.Select(l => l.Score), "Bad Loops");

        // Вывод плохих песен с известной тональностью, у которых определенная тональность не равна KnownTonality
        Console.WriteLine("\nBad Songs with Incorrectly Known Tonality:");
        int incorrectDetectionCount = 0;
        foreach (var song in incorrectTonalitySongs)
        {
            double[] calculatedProbabilities = analyzer.CalculateProbabilities(song.Id, true);
            int predictedTonality = GetPredictedTonality(calculatedProbabilities);
            if (!song.SecretTonalities.Contains(predictedTonality))
            {
                incorrectDetectionCount++;
                Console.WriteLine($"Song {song.Id}, Known Tonality: {song.KnownTonality}, Predicted Tonality: {predictedTonality}, Secret Tonalities: {string.Join(",", song.SecretTonalities)}, Score: {song.Score:F4}");
            }
        }
        Console.WriteLine($"Incorrectly Detected Songs with Incorrectly Known Tonality: {incorrectDetectionCount} / {incorrectTonalitySongs.Count()} ({(double)incorrectDetectionCount / incorrectTonalitySongs.Count():P2})");

        // Вывод правильности определения тональностей для песен и циклов
        Console.WriteLine("\nAccuracy of Detected Tonalities:");
        int correctSongDetections = songs.Values.Count(s => s.SecretTonalities.Contains(GetPredictedTonality(analyzer.CalculateProbabilities(s.Id, true))));
        int correctLoopDetections = loops.Values.Count(l => l.SecretTonalities.Contains(GetPredictedTonality(analyzer.CalculateProbabilities(l.Id, false))));

        Console.WriteLine($"Correctly Detected Song Tonalities: {correctSongDetections} / {songs.Count} ({(double)correctSongDetections / songs.Count:P2})");
        Console.WriteLine($"Correctly Detected Loop Tonalities: {correctLoopDetections} / {loops.Count} ({(double)correctLoopDetections / loops.Count:P2})");
    }

    private static int GetPredictedTonality(double[] probabilities)
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
