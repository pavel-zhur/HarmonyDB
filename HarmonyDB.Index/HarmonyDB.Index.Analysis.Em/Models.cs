public interface ISource
{
    string Id { get; set; }
    double[,] TonalityProbabilities { get; set; } // [TonicCount, ScaleCount]
    (double TonicScore, double ScaleScore) Score { get; set; }
}

public interface ISong : ISource
{
    bool IsTonalityKnown { get; set; }
    (int Tonic, Scale Scale) KnownTonality { get; set; }
}

public interface ILoop : ISource
{
    int SongCount { get; set; }
}

public class Song : ISong
{
    public string Id { get; set; }
    public double[,] TonalityProbabilities { get; set; } = new double[Constants.TonicCount, Constants.ScaleCount];
    public (double TonicScore, double ScaleScore) Score { get; set; } = (1.0, 1.0);
    public bool IsTonalityKnown { get; set; }
    public (int Tonic, Scale Scale) KnownTonality { get; set; }
    public (int Tonic, Scale Scale)[] SecretTonalities { get; set; }
    public bool IsKnownTonalityIncorrect { get; set; }
}

public class Loop : ILoop
{
    public string Id { get; set; }
    public double[,] TonalityProbabilities { get; set; } = new double[Constants.TonicCount, Constants.ScaleCount];
    public (double TonicScore, double ScaleScore) Score { get; set; } = (1.0, 1.0);
    public int SongCount { get; set; } = 0;
    public (int Tonic, Scale Scale)[] SecretTonalities { get; set; }
}

public class LoopLink
{
    public string SongId { get; set; }
    public string LoopId { get; set; }
    public int Shift { get; set; }
    public int Count { get; set; }
}