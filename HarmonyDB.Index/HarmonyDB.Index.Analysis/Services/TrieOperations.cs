using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.Index.Trie;

namespace HarmonyDB.Index.Analysis.Services;

public class TrieOperations
{
    public void Insert(TrieNode root, ReadOnlyMemory<(byte rootDelta, ChordType toType)> progression, int? limit)
    {
        // Insert all suffixes of the progression into the trie
        for (var start = 0; start < progression.Length; start++)
        {
            var node = root;
            for (var i = start; i < progression.Length && (!limit.HasValue || i < start + limit); i++)
            {
                var currentMovement = progression.Span[i];
                
                node = node.GetOrCreate(currentMovement.rootDelta, currentMovement.toType);
                node.Increment();
            }
        }
    }

    public Dictionary<(byte rootDelta, ChordType toType), int>? Search(TrieNode root, ReadOnlyMemory<(byte rootDelta, ChordType toType)> subsequence)
    {
        var node = root;
        foreach (var (rootDelta, toType) in subsequence.Span)
        {
            var child = node.Get(rootDelta, toType);
            if (child == null)
            {
                return null; // Subsequence not found
            }

            node = child;
        }
        
        return node.All.ToDictionary(pair => (pair.rootDelta, pair.toType), pair => pair.value.Frequency);
    }
}