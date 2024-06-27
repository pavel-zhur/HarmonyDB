using HarmonyDB.Common.FullTextSearch;

namespace OneShelf.Common.Songs.FullTextSearch;

public static class FullTextSearchLogic
{
    public static (List<T> found, bool isUserSpecific) Find<T>(
        IReadOnlyCollection<(T song, IReadOnlyCollection<string> words)> cache,
        string query,
        long userId)
        where T : class, ISearchableSong
    {
        var terms = query
            .ToLowerInvariant()
            .ToSearchSyntax()
            .Split(FullTextSearchExtensions.Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (!terms.Any())
        {
            return (
                cache
                    .Select(x => (
                        x.song,
                        level: x.song.Likes.Where(x => x.UserId == userId).Select(x => (byte?)x.Level).Max(),
                        rating: x.song.Likes.GetRating()))
                    .OrderByDescending(x => x.level)
                    .ThenByDescending(x => x.rating)
                    .ThenByDescending(x => x.song.Index)
                    .Select(x => x.song)
                    .ToList(),
                true);
        }

        List<(T song, int fullyFound, int beginningFound)> found = new();
        foreach (var (song, words) in cache)
        {
            var fullyFound = 0;
            var beginningFound = 0;

            foreach (var term in terms)
            {
                if (words.Contains(term))
                {
                    fullyFound++;
                    goto termMatch;
                }

                foreach (var word in words)
                {
                    if (word.StartsWith(term))
                    {
                        beginningFound++;
                        goto termMatch;
                    }

                    if (word.Contains(term))
                    {
                        goto termMatch;
                    }
                }

                goto songNotMatch;

            termMatch:;
            }

            found.Add((song, fullyFound, beginningFound));

        songNotMatch:;
        }

        var foundSongs = found
            .OrderByDescending(x => x.fullyFound)
            .ThenByDescending(x => x.beginningFound)
            .ThenByDescending(x => x.song.Likes.Select(x => x.UserId).Distinct().Count())
            .ThenByDescending(x => x.song.Index)
            .Select(x => x.song);

        var foundByIndex = int.TryParse(query.Trim(), out var value)
            ? cache.SingleOrDefault(x => x.song.Index == value).song
            : null;

        return (
            (foundByIndex != null
                ? foundSongs.Prepend(foundByIndex)
                : foundSongs)
            .ToList(),
            false);
    }

    public static IReadOnlyCollection<(T song, IReadOnlyCollection<string> words)> BuildCache<T>(
        IEnumerable<T> songs,
        Func<T, IEnumerable<ISearchableArtist>> artists)
        where T : ISearchableSong
    {
        return songs
            .Select(x => (x,
                (IReadOnlyCollection<string>)artists(x)
                    .SelectMany(a => a.Synonyms.Prepend(a.Name))
                    .Append(x.Title)
                    .Append(x.AdditionalKeywords)
                    .Select(x => x?.SearchSyntaxRemoveSeparators())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .SelectMany(x => x
                        .ToSearchSyntax()
                        .Split(FullTextSearchExtensions.Separators, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x)))
                    .ToHashSet()))
            .ToList();
    }
}