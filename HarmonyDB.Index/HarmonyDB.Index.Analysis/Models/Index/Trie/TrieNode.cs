namespace HarmonyDB.Index.Analysis.Models.Index.Trie;

public class TrieNode
{
    private volatile int _frequency;
    private readonly Dictionary<(byte rootDelta, ChordType toType), TrieNode> _children = new();
    
    public int Frequency => _frequency;
    public IReadOnlyDictionary<(byte rootDelta, ChordType toType), TrieNode> Children => _children;

    public void Increment(int delta = 1) => Interlocked.Add(ref _frequency, delta);

    public TrieNode GetSafe((byte rootDelta, ChordType toType) key)
    {
        if (Children.TryGetValue(key, out var value))
        {
            return value;
        }

        lock (_children)
        {
            if (_children.TryGetValue(key, out value))
            {
                return value;
            }
            
            return _children[key] = new();
        }
    }
}