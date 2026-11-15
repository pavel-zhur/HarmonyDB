namespace HarmonyDB.Index.Analysis.Models.Ids;

/// <summary>
/// The items of one collection (songs or loops), each a number of an <see cref="IdTable"/>, in the collection's order:
/// the number at each position and the position of each number.
/// </summary>
public class IdPositions
{
    private readonly int[] _numbers;
    private readonly int[] _positions;

    /// <param name="numbers">The numbers in the collection's order.</param>
    /// <exception cref="ArgumentException">A number repeats.</exception>
    public IdPositions(int[] numbers)
    {
        _numbers = numbers;
        _positions = new int[numbers.Length == 0 ? 0 : numbers.Max() + 1];
        Array.Fill(_positions, -1);
        for (var position = 0; position < numbers.Length; position++)
        {
            ref var existing = ref _positions[numbers[position]];
            if (existing != -1) throw new ArgumentException($"The number {numbers[position]} repeats.", nameof(numbers));
            existing = position;
        }
    }

    public int Count => _numbers.Length;

    public int GetNumber(int position) => _numbers[position];

    public bool TryGetPosition(int number, out int position)
    {
        position = number < _positions.Length ? _positions[number] : -1;
        return position != -1;
    }

    public bool Contains(int number) => TryGetPosition(number, out _);

    /// <exception cref="KeyNotFoundException">The number is not in the collection.</exception>
    public int GetPosition(int number)
        => TryGetPosition(number, out var position) ? position : throw new KeyNotFoundException($"The number {number} is not in the collection.");
}
