namespace OneShelf.Common;

public static class ExtensionStructOverloads
{
    public static IEnumerable<(T? previous, T current)> WithPrevious<T>(this IEnumerable<T> source)
        where T : struct
    {
        T? previous = null;
        foreach (var x in source)
        {
            yield return (previous, x);

            previous = x;
        }
    }

    public static List<(List<T> chunk, TCriterium criterium)> ToChunks<T, TCriterium>(this IEnumerable<T> source, Func<T, TCriterium> criteriumGetter)
        where TCriterium : struct
    {
        TCriterium? previous = null;
        var counter = 0;

        return source
            .GroupBy(x =>
            {
                var criterium = criteriumGetter(x);
                if (previous.HasValue && !previous.Equals(criterium))
                {
                    counter++;
                }

                previous = criterium;
                return (criterium, counter);
            })
            .Select(g => (g.ToList(), g.Key.criterium))
            .ToList();
    }
}