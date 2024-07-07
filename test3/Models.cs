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