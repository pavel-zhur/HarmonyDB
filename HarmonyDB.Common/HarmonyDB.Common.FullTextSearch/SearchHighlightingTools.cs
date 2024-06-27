namespace HarmonyDB.Common.FullTextSearch;

public static class SearchHighlightingTools
{
    public static IEnumerable<(string fragment, bool isHighlighted)> GetFragments(string? query, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var split = query?.ToLowerInvariant().ToSearchSyntax().Split(FullTextSearchExtensions.Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split?.Length is null or 0)
        {
            yield return (text, false);
            yield break;
        }

        var remaining = text.ToLowerInvariant().ToSearchSyntax().SearchSyntaxRemoveSeparators();
        var rendering = text;
        var anyToRemove = rendering.SearchSyntaxAnySeparatorsToRemove();

        while (true)
        {
            var first = split
                .Select(x => (x, index: (int?)remaining.IndexOf(x, StringComparison.Ordinal)))
                .Where(x => x.index > -1)
                .OrderBy(x => x.index)
                .FirstOrDefault();

            var index = first.index;

            if (index == null)
            {
                yield return (remaining, false);
                yield break;
            }

            var updatedIndex = index.Value;

            if (updatedIndex > 0 && anyToRemove)
            {
                var expected = remaining.Substring(0, index.Value);
                var renderingTests = rendering.ToLowerInvariant().ToSearchSyntax();
                while (true)
                {
                    var test = renderingTests.Substring(0, updatedIndex).SearchSyntaxRemoveSeparators();
                    if (test == expected) break;

                    updatedIndex++;
                    if (updatedIndex > 100) throw new("Protection. Failed.");
                }
            }

            var matchLength = first.x.Length;
            var updatedMatchLength = matchLength;

            if (anyToRemove)
            {
                var expected = remaining.Substring(index.Value, matchLength);
                var renderingTests = rendering.Substring(updatedIndex).ToLowerInvariant().ToSearchSyntax();
                while (true)
                {
                    var test = renderingTests.Substring(0, updatedMatchLength).SearchSyntaxRemoveSeparators();
                    if (test == expected) break;

                    updatedMatchLength++;
                    if (updatedMatchLength > 100) throw new("Protection. Failed.");
                }
            }

            if (index == 0)
            {
                yield return (rendering.Substring(0, updatedMatchLength), true);
            }
            else
            {
                yield return (rendering.Substring(0, updatedIndex), false);
                yield return (rendering.Substring(updatedIndex, updatedMatchLength), true);
            }

            remaining = remaining.Substring(index.Value + matchLength);
            rendering = rendering.Substring(updatedIndex + updatedMatchLength);
            if (remaining == string.Empty) yield break;
        }
    }
}