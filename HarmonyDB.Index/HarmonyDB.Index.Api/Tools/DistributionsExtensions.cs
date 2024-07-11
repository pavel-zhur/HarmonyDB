namespace HarmonyDB.Index.Api.Tools;

public static class DistributionsExtensions
{
    public static TValue[] GetPercentiles<T, TValue>(this IEnumerable<T> values, Func<T, TValue> selector)
    {
        var ordered = values.Select(selector).OrderBy(x => x).ToArray();
        var percentiles = new TValue[11];
        for (var i = 0; i <= 10; i++)
        {
            percentiles[i] = ordered[i * (ordered.Length - 1) / 10];
        }

        return percentiles;
    }
}