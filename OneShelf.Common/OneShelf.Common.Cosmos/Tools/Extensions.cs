using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace OneShelf.Common.Cosmos.Tools;

public static class Extensions
{
    public static async Task<List<T>> ReadAll<T>(this IQueryable<T> source, CancellationToken cancellationToken = default, Action<int>? progressUpdate = null)
    {
	    using var iterator = source.ToFeedIterator();
	    return await iterator.ReadAllAndDispose(cancellationToken, progressUpdate);
    }

    public static async Task<List<T>> ReadAllAndDispose<T>(this FeedIterator<T> iterator, CancellationToken cancellationToken = default, Action<int>? progressUpdate = null)
    {
	    var result = new List<T>();
	    while (iterator.HasMoreResults)
	    {
		    result.AddRange(await iterator.ReadNextAsync(cancellationToken));
		    progressUpdate?.Invoke(result.Count);
	    }

	    iterator.Dispose();

	    return result;
    }
}