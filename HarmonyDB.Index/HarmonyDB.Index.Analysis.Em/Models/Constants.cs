namespace HarmonyDB.Index.Analysis.Em.Models;

public static class Constants
{
    public const int TonicCount = 12; // Number of different tonics
    public const int ScaleCount = 2;  // Major and Minor

    public static IEnumerable<(int tonic, Scale scale)> Pairs
        => Indices.Select(x => (x.tonic, (Scale)x.scale));

    public static IEnumerable<(int tonic, int scale)> Indices
        => Enumerable
            .Range(0, TonicCount)
            .SelectMany(i => Enumerable.Range(0, ScaleCount).Select(j => (i, j)));

    public static int GetMajorTonic((int tonic, Scale scale) scale)
    {
        return scale.scale == Scale.Major ? scale.tonic : GetParallelScale(scale).tonic;
    }

    public static (int tonic, Scale scale) GetParallelScale((int tonic, Scale scale) scale)
    {
        return scale.scale == Scale.Major ? ((scale.tonic - 3 + TonicCount) % TonicCount, Scale.Minor) : ((scale.tonic + 3) % TonicCount, Scale.Major);
    }
}