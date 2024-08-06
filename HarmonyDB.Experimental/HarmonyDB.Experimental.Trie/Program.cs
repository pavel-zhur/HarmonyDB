public class TrieNode
{
    public Dictionary<char, TrieNode> Children { get; private set; }
    // Stores frequencies of elements following the subsequence ending at this node
    public Dictionary<char, int> FollowingFrequencies { get; private set; }

    public TrieNode()
    {
        Children = new();
        FollowingFrequencies = new();
    }
}

public class Trie
{
    private readonly TrieNode _root;

    public Trie()
    {
        _root = new();
    }

    public void Insert(string sequence)
    {
        // Insert all suffixes of the sequence into the trie
        for (var start = 0; start < sequence.Length; start++)
        {
            var node = _root;
            for (var i = start; i < sequence.Length; i++)
            {
                var currentChar = sequence[i];
                if (!node.Children.ContainsKey(currentChar))
                {
                    node.Children[currentChar] = new();
                }
                node = node.Children[currentChar];

                if (i + 1 < sequence.Length)
                {
                    var nextChar = sequence[i + 1];
                    if (node.FollowingFrequencies.ContainsKey(nextChar))
                    {
                        node.FollowingFrequencies[nextChar]++;
                    }
                    else
                    {
                        node.FollowingFrequencies[nextChar] = 1;
                    }
                }
            }
        }
    }

    public Dictionary<char, int> Search(string subsequence)
    {
        var node = _root;
        foreach (var c in subsequence)
        {
            if (!node.Children.ContainsKey(c))
            {
                return null; // Subsequence not found
            }
            node = node.Children[c];
        }
        return node.FollowingFrequencies;
    }
}

class Program
{
    static void Main()
    {
        var trie = new Trie();
        trie.Insert("sabc");
        trie.Insert("sabd");
        trie.Insert("sacd");
        trie.Insert("sbcd");

        while (true)
        {
            var frequencies = trie.Search(Console.ReadLine());
            if (frequencies != null)
            {
                foreach (var pair in frequencies)
                {
                    Console.WriteLine($"Element '{pair.Key}' follows with frequency: {pair.Value}");
                }
            }
            else
            {
                Console.WriteLine("Subsequence not found.");
            }
        }
    }
}