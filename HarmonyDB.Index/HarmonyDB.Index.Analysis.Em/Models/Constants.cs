namespace HarmonyDB.Index.Analysis.Em.Models;

public static class Constants
{
    public const int TonicCount = 12; // Number of different tonics
    public const int ScaleCount = 2;  // Major and Minor

    public static IReadOnlyList<(byte tonic, byte scale)> Indices { get; }
        = Enumerable
            .Range(0, TonicCount)
            .SelectMany(i => Enumerable.Range(0, ScaleCount).Select(j => ((byte)i, (byte)j)))
            .ToList();

    public static IReadOnlyList<(byte tonic, Scale scale)> Pairs { get; }
        = Indices.Select(x => ((byte)x.tonic, (Scale)x.scale)).ToList();

    public static int GetMajorTonic((byte tonic, Scale scale) scale, bool isSong)
    {
        return scale.scale == Scale.Major ? scale.tonic : GetParallelScale(scale, isSong).tonic;
    }

    public static (byte tonic, Scale scale) GetParallelScale((byte tonic, Scale scale) scale, bool isSong)
    {
        // todo: write unit tests
        return scale.scale == Scale.Major
            ? ((byte)((scale.tonic + (isSong ? -3 : 3) + TonicCount) % TonicCount), Scale.Minor) 
            : ((byte)((scale.tonic + (isSong ? 3 : -3) + TonicCount) % TonicCount), Scale.Major);
    }
}