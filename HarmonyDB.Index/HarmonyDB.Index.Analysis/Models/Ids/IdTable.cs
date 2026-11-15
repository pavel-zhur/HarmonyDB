namespace HarmonyDB.Index.Analysis.Models.Ids;

/// <summary>
/// Ids (of songs or of loops) known by numbers: each id string is kept here once, and what refers to it keeps its number.
/// Numbers are given in the order the ids are first added, and never change.
/// Adding and finding a number are safe from several threads; reading the id of a number takes no lock.
/// </summary>
public class IdTable
{
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _numbers = new();
    private string[] _ids = new string[1024];

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _numbers.Count;
            }
        }
    }

    public string this[int number] => Volatile.Read(ref _ids)[number];

    public int GetOrAdd(string id)
    {
        lock (_lock)
        {
            if (_numbers.TryGetValue(id, out var number)) return number;

            number = _numbers.Count;
            if (number == _ids.Length)
            {
                var ids = new string[_ids.Length * 2];
                Array.Copy(_ids, ids, number);
                ids[number] = id;
                Volatile.Write(ref _ids, ids);
            }
            else
            {
                _ids[number] = id;
            }

            _numbers.Add(id, number);
            return number;
        }
    }

    public bool TryGetNumber(string id, out int number)
    {
        lock (_lock)
        {
            return _numbers.TryGetValue(id, out number);
        }
    }

    /// <exception cref="KeyNotFoundException">The id is not known.</exception>
    public int GetNumber(string id)
        => TryGetNumber(id, out var number) ? number : throw new KeyNotFoundException($"The id '{id}' is not known.");
}
