using HarmonyDB.Common.FullTextSearch;
using HarmonyDB.Index.BusinessLogic.Caches.Bases;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.BusinessLogic.Caches;

public class FullTextSearchCache : MemoryCacheBase<IndexHeaders, FullTextSearch>
{
    private readonly IndexHeadersCache _indexHeadersCache;

    public FullTextSearchCache(ILogger<MemoryCacheBase<IndexHeaders, FullTextSearch>> logger, IndexHeadersCache indexHeadersCache)
        : base(logger)
    {
        _indexHeadersCache = indexHeadersCache;
    }

    protected override string Key => "FullTextSearch";

    protected override async Task<IndexHeaders> GetInputModel() => await _indexHeadersCache.Get();

    protected override FullTextSearch GetPresentationModel(IndexHeaders inputModel)
    {
        // the words of each header, as word numbers, header after header
        var words = new Dictionary<string, int>();
        var wordsCounts = new List<int>();
        var headersWords = new List<int>();
        var headersWordsStarts = new int[inputModel.Count + 1];
        for (var position = 0; position < inputModel.Count; position++)
        {
            var h = inputModel.Get(position);
            foreach (var w in (h.Artists ?? Enumerable.Empty<string>())
                         .Append(h.Title)
                         .Where(x => x != null)
                         .Select(x => x!.SearchSyntaxRemoveSeparators())
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .SelectMany(x => x.ToSearchSyntax()
                             .Split(FullTextSearchExtensions.Separators, StringSplitOptions.RemoveEmptyEntries)
                             .Select(x => x.Trim())
                             .Where(x => !string.IsNullOrWhiteSpace(x))))
            {
                if (!words.TryGetValue(w, out var word))
                {
                    word = words[w] = words.Count;
                    wordsCounts.Add(0);
                }

                wordsCounts[word]++;
                headersWords.Add(word);
            }

            headersWordsStarts[position + 1] = headersWords.Count;
        }

        // the headers of each word, word after word
        var wordsHeadersStarts = new int[words.Count + 1];
        for (var word = 0; word < words.Count; word++)
        {
            wordsHeadersStarts[word + 1] = wordsHeadersStarts[word] + wordsCounts[word];
        }

        var next = wordsHeadersStarts[..words.Count];
        var wordsHeaders = new int[headersWords.Count];
        for (var position = 0; position < inputModel.Count; position++)
        {
            for (var i = headersWordsStarts[position]; i < headersWordsStarts[position + 1]; i++)
            {
                wordsHeaders[next[headersWords[i]]++] = position;
            }
        }

        return new(inputModel, words.OrderBy(x => x.Value).Select(x => x.Key).ToArray(), wordsHeadersStarts, wordsHeaders);
    }
}
