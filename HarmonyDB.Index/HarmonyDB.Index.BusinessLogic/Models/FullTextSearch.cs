using HarmonyDB.Common.FullTextSearch;
using HarmonyDB.Source.Api.Model.V1;

namespace HarmonyDB.Index.BusinessLogic.Models;

public class FullTextSearch
{
    private readonly ILookup<string, IndexHeader> _headersByWords;

    public FullTextSearch(ILookup<string, IndexHeader> headersByWords)
    {
        _headersByWords = headersByWords;
    }

    public List<IndexHeader> Find(string query)
    {
        var terms = query
            .ToSearchSyntax()
            .Split(FullTextSearchExtensions.Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

        if (!terms.Any()) return [];

        return terms.SelectMany(t => _headersByWords
                .Select(w =>
                    (w, match: w.Key == t
                        ? WordMatch.Full
                        : w.Key.Contains(t, StringComparison.InvariantCulture)
                            ? w.Key.StartsWith(t, StringComparison.InvariantCulture)
                                ? WordMatch.Beginning
                                : WordMatch.Middle
                            : WordMatch.None))
                .Where(w => w.match > WordMatch.None)
                .SelectMany(w => w.w.Select(h => (h, w.match, t))))
            .GroupBy(h => (h.t, h.h))
            .Select(g => g.MaxBy(w => w.match))
            .GroupBy(h => h.h)
            .Where(g => g.Count() == terms.Count)
            .Select(g => (header: g.Key, full: g.Count(h => h.match == WordMatch.Full),
                beginning: g.Count(h => h.match == WordMatch.Beginning)))
            .OrderByDescending(x => x.full)
            .ThenByDescending(x => x.beginning)
            .ThenByDescending(x => x.header.Rating)
            .Select(x => x.header)
            .ToList();
    }

    private enum WordMatch
    {
        None,
        Middle,
        Beginning,
        Full,
    }
}