namespace HarmonyDB.Index.Api.Tools;

public static class DistributionsExtensions
{
    public static TValue[] GetPercentiles<T, TValue>(this IEnumerable<T> values, Func<T, TValue> selector)
    {
        var ordered = values.Select(selector).OrderBy(x => x).ToArray();
        var percentiles = new TValue[11];
        if (!ordered.Any()) return percentiles;

        for (var i = 0; i <= 10; i++)
        {
            percentiles[i] = ordered[i * (ordered.Length - 1) / 10];
        }

        return percentiles;
    }

    public static TValue[] GetWeightedPercentiles<T, TValue>(this IEnumerable<T> values, Func<T, (TValue value, float weight)> selector)
    {
        var weightedValues = values.Select(selector)
                                   .OrderBy(x => x.value)
                                   .ToList();

        var totalWeight = weightedValues.Sum(x => x.weight);
        var percentiles = new TValue[11];
        var cumulativeWeight = 0f;
        var percentileIndex = 0;

        foreach (var (value, weight) in weightedValues)
        {
            cumulativeWeight += weight;
            while (cumulativeWeight >= (percentileIndex * totalWeight) / 10)
            {
                percentiles[percentileIndex] = value;
                percentileIndex++;
                if (percentileIndex > 10) break;
            }
            if (percentileIndex > 10) break;
        }

        return percentiles;
    }
}
