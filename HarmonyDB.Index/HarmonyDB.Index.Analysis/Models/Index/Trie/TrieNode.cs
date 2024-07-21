namespace HarmonyDB.Index.Analysis.Models.Index.Trie;

public class TrieNode
{
    private volatile int _frequency;
    private ((byte rootDelta, ChordType toType) key, TrieNode value)[] _children = [];
    private readonly ReaderWriterLockSlim _lock = new();
    
    public int Frequency => _frequency;

    public void Increment(int delta = 1) => Interlocked.Add(ref _frequency, delta);

    public IReadOnlyList<((byte rootDelta, ChordType toType) key, TrieNode value)> All => _children;
    
    public TrieNode? Get((byte rootDelta, ChordType toType) key)
    {
        return _children.SingleOrDefault(x => x.key == key).value;
    }

    public TrieNode GetOrCreate((byte rootDelta, ChordType toType) key)
    {
        _lock.EnterUpgradeableReadLock();

        var write = false;
        try
        {
            var value = _children.SingleOrDefault(x => x.key == key).value;
            if (value != null)
                return value;
            
            _lock.EnterWriteLock();
            write = true;

            value = _children.SingleOrDefault(x => x.key == key).value;
            if (value != null)
                return value;

            value = new();
            _children = _children.Append((key, value)).ToArray();
            return value;
        }
        finally
        {
            if (write)
            {
                _lock.ExitWriteLock();
            }

            _lock.ExitUpgradeableReadLock();
        }
    }
}