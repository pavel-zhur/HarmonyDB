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
}