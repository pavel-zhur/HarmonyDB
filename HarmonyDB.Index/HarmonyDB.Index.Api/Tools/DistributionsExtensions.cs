namespace HarmonyDB.Index.Api.Tools;

public static class DistributionsExtensions
{
    public static float[] GetPercentiles<T>(this IEnumerable<T> values, Func<T, float> selector)
    {
        var ordered = values.Select(selector).OrderBy(x => x).ToArray();
        var percentiles = new float[11];
        for (var i = 0; i <= 10; i++)
        {
            percentiles[i] = ordered[i * (ordered.Length - 1) / 10];
        }

        return percentiles;
    }

    public static float?[] GetPercentiles<T>(this IEnumerable<T> values, Func<T, float?> selector)
    {
        var ordered = values.Select(selector).OrderBy(x => x).ToArray();
        var percentiles = new float?[11];
        for (var i = 0; i <= 10; i++)
        {
            percentiles[i] = ordered[i * (ordered.Length - 1) / 10];
        }

        return percentiles;
    }
}