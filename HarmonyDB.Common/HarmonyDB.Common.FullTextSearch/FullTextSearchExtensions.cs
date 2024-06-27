namespace HarmonyDB.Common.FullTextSearch;

public static class FullTextSearchExtensions
{
    public static readonly char[] Separators =
    {
        '-',
        ',',
        ' ',
        '\r',
        '\n',
        '\'',
    };

    public static string ToSearchSyntax(this string text) => text.ToLowerInvariant().Replace("ё", "е");

    public static string SearchSyntaxRemoveSeparators(this string text) => text.Replace("'", "");

    public static bool SearchSyntaxAnySeparatorsToRemove(this string text) => text.Contains('\'');
}