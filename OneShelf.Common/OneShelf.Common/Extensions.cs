namespace OneShelf.Common;

public static class Extensions
{
    public static TDestination SelectSingle<TSource, TDestination>(this TSource source,
        Func<TSource, TDestination> func) => func(source);

    public static IEnumerable<T> Once<T>(this T first)
    {
        yield return first;
    }

    public static IReadOnlyList<T> AsIReadOnlyList<T>(this IReadOnlyList<T> list) => list;
    
    public static IReadOnlyDictionary<TKey, TValue> AsIReadOnlyDictionary<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary) => dictionary;

    public static IEnumerable<T?> AsNullable<T>(this IEnumerable<T> enumeration)
        where T : struct
        => enumeration.Select(x => (T?)x);

    public static bool AnyDuplicates<T>(this IEnumerable<T> source, out T? firstDuplicate)
        => source.AnyDuplicates(x => x, out firstDuplicate);

    public static bool AnyDuplicates<T>(this IEnumerable<T> source)
        => source.AnyDuplicates(x => x, out _);

    public static bool AnyDuplicates<TSource, TItem>(this IEnumerable<TSource> source, Func<TSource, TItem> selector, out TItem? firstDuplicate)
    {
        var group = source.GroupBy(selector).FirstOrDefault(x => x.Count() > 1);

        if (group == null)
        {
            firstDuplicate = default;
            return false;
        }

        firstDuplicate = group.Key;
        return true;
    }

    public static TValue? Safe<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
        where TValue : struct
        => dictionary.TryGetValue(key, out var value) ? value : null;

    public static IEnumerable<(T x, int i)> WithIndices<T>(this IEnumerable<T> source) =>
        source.Select((x, i) => (x, i));

    public static IEnumerable<(T x, int? i)> WithIndicesNullable<T>(this IEnumerable<T> source) =>
        source.Select((x, i) => (x, (int?)i));

    public static IEnumerable<(T x, bool isLast)> WithIsLast<T>(this IEnumerable<T> source)
    {
        T? previous = default;
        var isPreviousSet = false;
        foreach (var item in source)
        {
            if (isPreviousSet)
            {
                yield return (previous!, false);
            }

            isPreviousSet = true;
            previous = item;
        }

        if (isPreviousSet)
        {
            yield return (previous!, true);
        }
    }

    public static IEnumerable<(T x, bool isFirst)> WithIsFirst<T>(this IEnumerable<T> source)
    {
        var isFirst = true;
        foreach (var item in source)
        {
            yield return (item, isFirst);
            isFirst = false;
        }
    }

    public static IEnumerable<(T x, bool isFirst, bool isLast)> WithIsFirstLast<T>(this IEnumerable<T> source)
        => source.WithIsFirst().WithIsLast().Select(x => (x.x.x, x.x.isFirst, x.isLast));

    public static IEnumerable<(T? previous, T current)> WithPrevious<T>(this IEnumerable<T> source)
        where T : class
    {
        T? previous = null;
        foreach (var x in source)
        {
            yield return (previous, x);

            previous = x;
        }
    }

    public static IEnumerable<(T previous, T current)> AsPairs<T>(this IEnumerable<T> source)
    {
        T? previous = default;
        foreach (var (x, isFirst) in source.WithIsFirst())
        {
            if (isFirst)
            {
                previous = x;
                continue;
            }

            yield return (previous!, x);
            previous = x;
        }
    }

    public static List<(List<T> chunk, TCriterium? criterium)> ToChunks<T, TCriterium>(this IEnumerable<T> source, Func<T, TCriterium?> criteriumGetter)
        where TCriterium : class
    {
        TCriterium? previous = null;
        var previousSet = false;
        var counter = 0;

        return source
            .GroupBy(x =>
            {
                var criterium = criteriumGetter(x);
                if (previousSet && (!previous?.Equals(criterium) ?? criterium != null))
                {
                    counter++;
                }

                previousSet = true;
                previous = criterium;
                return (criterium, counter);
            })
            .Select(g => (g.ToList(), g.Key.criterium))
            .ToList();
    }

    public static List<List<T>> ToChunksByShouldStartNew<T>(this IEnumerable<T> source, Func<T, bool> shouldStartNewGetter)
    {
        var counter = 0;

        return source
            .GroupBy(x =>
            {
                var shouldStartNew = shouldStartNewGetter(x);
                if (shouldStartNew) return ++counter;
                return counter;
            })
            .Select(g => g.ToList())
            .ToList();
    }

    public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            hashSet.Add(item);
        }
    }

    public static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, IEnumerable<(TKey key, TValue value)> items, bool overwrite)
        where TKey : notnull
    {
        foreach (var (key, value) in items)
        {
            if (overwrite)
            {
                dictionary[key] = value;
            }
            else
            {
                dictionary.Add(key, value);
            }
        }
    }

    public static T? OnceAsNullable<T>(this T value)
        where T : struct
        => value;

    public static T? FirstOrNull<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        where T : struct
        => source.Select(x => (T?)x).FirstOrDefault(x => predicate(x!.Value));
}