namespace HarmonyDB.Index.Analysis.Em.Models;

public static class Constants
{
    public const int TonicCount = 12; // Number of different tonics
    public const int ScaleCount = 2;  // Major and Minor

    public static IEnumerable<(byte tonic, Scale scale)> Pairs
        => Indices.Select(x => ((byte)x.tonic, (Scale)x.scale));

    public static IEnumerable<(int tonic, int scale)> Indices
        => Enumerable
            .Range(0, TonicCount)
            .SelectMany(i => Enumerable.Range(0, ScaleCount).Select(j => (i, j)));

    public static Scale GetParallelScale(Scale scale)
    {
        return scale == Scale.Major ? Scale.Minor : Scale.Major;
    }
}