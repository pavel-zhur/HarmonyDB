namespace OneShelf.Common.Songs.Hashing;

public static class MyHashExtensions
{
    public static int CalculateHash(IEnumerable<int> values)
    {
        var hash = 269;
        foreach (var o in values)
        {
            hash = hash * 47 + o;
        }

        return hash;
    }
}