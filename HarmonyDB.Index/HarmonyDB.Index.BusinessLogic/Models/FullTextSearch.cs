using HarmonyDB.Common.FullTextSearch;
using HarmonyDB.Source.Api.Model.V1;

namespace HarmonyDB.Index.BusinessLogic.Models;

public class FullTextSearch
{
    private readonly IndexHeaders _headers;
    private readonly string[] _words;
    private readonly int[] _wordsHeadersStarts;
    private readonly int[] _wordsHeaders;

    /// <param name="headers">The headers searched.</param>
    /// <param name="words">The words, each once, in the order they first come.</param>
    /// <param name="wordsHeadersStarts">Where the headers of each word start in <paramref name="wordsHeaders"/>, and where the last ones end.</param>
    /// <param name="wordsHeaders">The positions of the headers of each word, in their order, a header as many times as it has the word.</param>
    public FullTextSearch(IndexHeaders headers, string[] words, int[] wordsHeadersStarts, int[] wordsHeaders)
    {
        _headers = headers;
        _words = words;
        _wordsHeadersStarts = wordsHeadersStarts;
        _wordsHeaders = wordsHeaders;
    }

    public List<IndexHeader> Find(string query)
    {
        var terms = query
            .ToSearchSyntax()
            .Split(FullTextSearchExtensions.Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

        if (!terms.Any()) return [];

        return terms.SelectMany(t => Enumerable.Range(0, _words.Length)
                .Select(w =>
                    (w, match: _words[w] == t
                        ? WordMatch.Full
                        : _words[w].Contains(t, StringComparison.InvariantCulture)
                            ? _words[w].StartsWith(t, StringComparison.InvariantCulture)
                                ? WordMatch.Beginning
                                : WordMatch.Middle
                            : WordMatch.None))
                .Where(w => w.match > WordMatch.None)
                .SelectMany(w => GetHeaders(w.w).Select(h => (h, w.match, t))))
            .GroupBy(h => (h.t, h.h))
            .Select(g => g.MaxBy(w => w.match))
            .GroupBy(h => h.h)
            .Where(g => g.Count() == terms.Count)
            .Select(g => (header: g.Key, full: g.Count(h => h.match == WordMatch.Full),
                beginning: g.Count(h => h.match == WordMatch.Beginning)))
            .OrderByDescending(x => x.full)
            .ThenByDescending(x => x.beginning)
            .ThenByDescending(x => _headers.GetRating(x.header))
            .Select(x => _headers.Get(x.header))
            .ToList();
    }

    private IEnumerable<int> GetHeaders(int word)
    {
        for (var i = _wordsHeadersStarts[word]; i < _wordsHeadersStarts[word + 1]; i++)
        {
            yield return _wordsHeaders[i];
        }
    }

    private enum WordMatch
    {
        None,
        Middle,
        Beginning,
        Full,
    }
}
