using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.Index.Trie;

namespace HarmonyDB.Index.Analysis.Services;

public class TrieOperations
{
    public void Insert(TrieNode root, ReadOnlyMemory<(byte rootDelta, ChordType toType)> progression)
    {
        // Insert all suffixes of the progression into the trie
        for (var start = 0; start < progression.Length; start++)
        {
            TrieNode node = root;
            for (var i = start; i < progression.Length && i < start + 8; i++)
            {
                var currentMovement = progression.Span[i];
                var key = (currentMovement.rootDelta, currentMovement.toType);

                node = node.GetOrCreate(key);
                node.Increment();
            }
        }
    }

    public Dictionary<(byte rootDelta, ChordType toType), int>? Search(TrieNode root, ReadOnlyMemory<(byte rootDelta, ChordType toType)> subsequence)
    {
        var node = root;
        foreach (var movement in subsequence.Span)
        {
            var key = (movement.rootDelta, movement.toType);
            var child = node.Get(key);
            if (child == null)
            {
                return null; // Subsequence not found
            }

            node = child;
        }
        
        return node.All.ToDictionary(pair => pair.key, pair => pair.value.Frequency);
    }
}