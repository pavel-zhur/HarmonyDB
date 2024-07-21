namespace HarmonyDB.Index.Analysis.Models.Index.Trie;

public class TrieNode
{
    private TrieNode(bool readOnly)
    {
        if (!readOnly)
        {
            _lock = new();
        }
    }
    
    private volatile int _frequency;
    private ((byte rootDelta, ChordType toType) key, TrieNode value)[] _children = [];
    private readonly ReaderWriterLockSlim? _lock;
    
    public int Frequency => _frequency;

    public void Increment(int delta = 1) => Interlocked.Add(ref _frequency, delta);

    public IReadOnlyList<((byte rootDelta, ChordType toType) key, TrieNode value)> All => _children;
    
    public TrieNode? Get((byte rootDelta, ChordType toType) key)
    {
        return _children.SingleOrDefault(x => x.key == key).value;
    }

    public TrieNode GetOrCreate((byte rootDelta, ChordType toType) key)
    {
        if (_lock == null)
        {
            throw new InvalidOperationException("Readonly mode.");
        }
        
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

            value = new(false);
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
    
    public static TrieNode NewRoot() => new(false);

    public static TrieNode Deserialize(BinaryReader reader)
    {
        var frequency = reader.ReadInt32();
        var childrenCount = reader.ReadByte();
        var children = new ((byte rootDelta, ChordType toType) key, TrieNode value)[childrenCount];
        for (var i = 0; i < childrenCount; i++)
        {
            var rootDelta = reader.ReadByte();
            var toType = (ChordType)reader.ReadByte();
            var value = Deserialize(reader);
            children[i] = ((rootDelta, toType), value);
        }

        return new(true)
        {
            _children = children,
            _frequency = frequency,
        };
    }
    
    public void Serialize(BinaryWriter writer)
    {
        checked
        {
            writer.Write(_frequency);
            writer.Write((byte)_children.Length);
            foreach (var ((rootDelta, toType), value) in _children)
            {
                writer.Write(rootDelta);
                writer.Write((byte)toType);
                value.Serialize(writer);
            }
        }
    }
}