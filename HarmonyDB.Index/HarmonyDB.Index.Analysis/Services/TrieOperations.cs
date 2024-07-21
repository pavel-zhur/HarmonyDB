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
            for (var i = start; i < progression.Length; i++)
            {
                var currentMovement = progression.Span[i];
                var key = (currentMovement.rootDelta, currentMovement.toType);

                node = node.GetSafe(key);
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
            if (!node.Children.TryGetValue(key, out var child))
            {
                return null; // Subsequence not found
            }

            node = child;
        }
        
        return node.Children.ToDictionary(pair => pair.Key, pair => pair.Value.Frequency);
    }
}