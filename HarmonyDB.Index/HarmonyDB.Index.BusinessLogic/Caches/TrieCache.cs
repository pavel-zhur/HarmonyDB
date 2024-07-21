using System.Runtime.InteropServices;
using HarmonyDB.Index.Analysis.Models.Index.Trie;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.BusinessLogic.Caches.Bases;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;

namespace HarmonyDB.Index.BusinessLogic.Caches;

public class TrieCache : BytesFileCacheBase<TrieNode>
{
    private readonly ProgressionsCache _progressionsCache;
    private readonly TrieOperations _trieOperations;

    public TrieCache(ILogger<FileCacheBase<byte[], TrieNode>> logger, IOptions<FileCacheBaseOptions> options, ProgressionsCache progressionsCache, TrieOperations trieOperations)
        : base(logger, options)
    {
        _progressionsCache = progressionsCache;
        _trieOperations = trieOperations;
    }

    protected override string Key => "Trie";

    protected override async Task<TrieNode> ToPresentationModel(byte[] fileModel)
    {
        using var stream = new MemoryStream(fileModel);
        using var reader = new BinaryReader(stream);
        return TrieNode.Deserialize(reader);
    }

    public async Task Rebuild(int? limit = null)
    {
        var progressions = (await _progressionsCache.Get())
            .Values
            .SelectSingle(x => limit.HasValue ? x.Take(limit.Value).OrderBy(_ => Random.Shared.NextDouble()) : x)
            .SelectMany(x => x.ExtendedHarmonyMovementsSequences)
            .ToList();
        
        var done = 0;
        var trie = TrieNode.NewRoot();

        await Parallel.ForEachAsync(progressions, (compactHarmonyMovementsSequence, _) =>
        {
            var currentlyDone = Interlocked.Increment(ref done);
            if (currentlyDone % 1000 == 0)
            {
                Console.WriteLine($"Done {done} progressions.");
            }

            _trieOperations.Insert(trie,
                MemoryMarshal.ToEnumerable(compactHarmonyMovementsSequence.Movements)
                    .Select(x => (x.RootDelta, x.ToType))
                    .Prepend(((byte)0, compactHarmonyMovementsSequence.Movements.Span[0].FromType))
                    .ToArray());

            return ValueTask.CompletedTask;
        });

        using var stream = new MemoryStream();
        await using var writer = new BinaryWriter(stream);
        trie.Serialize(writer);
        await StreamCompressSerialize(stream.ToArray());
    }
}