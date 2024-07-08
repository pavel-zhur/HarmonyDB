namespace HarmonyDB.Index.Analysis.Em.Models;

public static class Constants
{
    public const int TonicCount = 12; // Number of different tonics
    public const int ScaleCount = 2;  // Major and Minor

    public static Scale GetParallelScale(Scale scale)
    {
        return scale == Scale.Major ? Scale.Minor : Scale.Major;
    }
}