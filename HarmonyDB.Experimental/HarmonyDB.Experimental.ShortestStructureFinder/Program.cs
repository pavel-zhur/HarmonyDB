namespace HarmonyDB.Experimental.ShortestStructureFinder;

internal class Program
{
    static void Main()
    {
        var inputSequence = new List<SourceElement>
        {
            1, 2, 3, 1, 2, 3, 4, 5,
        }.Cast<IElement>().ToList();
        var initialPacking = new PackingVariant { CurrentSequence = inputSequence };
        var allPackings = new List<PackingVariant> { initialPacking };

        bool newPackingsFound;
        do
        {
            newPackingsFound = false;
            var newPackings = new List<PackingVariant>();

            foreach (var packing in allPackings.ToList())
            {
                newPackings.AddRange(FindCycles(packing));
                newPackings.AddRange(FindOrOr(packing));
                newPackings.AddRange(FindRepeatingChunks(packing));
            }

            if (newPackings.Count > 0)
            {
                allPackings = newPackings.Distinct().ToList();
                newPackingsFound = true;
            }

        } while (newPackingsFound);

        foreach (var packing in allPackings)
        {
            Console.WriteLine($"Текущая последовательность: {string.Join(", ", packing.CurrentSequence)}");
            foreach (var pattern in packing.Patterns)
            {
                Console.WriteLine($"Паттерн: {pattern.GetUnpackingTrace()} - {pattern.Description}");
            }
        }
    }

    static List<PackingVariant> FindCycles(PackingVariant packing)
    {
        var results = new List<PackingVariant>();
        var uniqueCycles = new HashSet<string>();

        for (var i = 0; i < packing.CurrentSequence.Count; i++)
        {
            for (var length = 1; i + length * 2 <= packing.CurrentSequence.Count; length++)
            {
                var sequence1 = packing.CurrentSequence.GetRange(i, length);
                var sequence2 = packing.CurrentSequence.GetRange(i + length, length);

                if (sequence1.SequenceEqual(sequence2, new ElementComparer()))
                {
                    var cycleKey = $"{i}-{length}";
                    if (!uniqueCycles.Contains(cycleKey))
                    {
                        uniqueCycles.Add(cycleKey);
                        var repeatingSequence = packing.CurrentSequence.GetRange(i, length);
                        var newPacking = packing.AddPattern(id => new SimplePattern(id, repeatingSequence, "Cycle"));
                        results.Add(newPacking);
                    }
                }
                // No break here, continue searching for longer cycles
            }
        }

        return results.Distinct().ToList();
    }

    static List<PackingVariant> FindOrOr(PackingVariant packing)
    {
        var results = new List<PackingVariant>();

        for (var i = 1; i < packing.CurrentSequence.Count - 2; i++)
        {
            for (var j = i + 1; j < packing.CurrentSequence.Count - 1; j++)
            {
                var branch1 = packing.CurrentSequence.GetRange(i, j - i);
                for (var k = j + 1; k < packing.CurrentSequence.Count; k++)
                {
                    var branch2 = packing.CurrentSequence.GetRange(j, k - j);
                    if (branch1.Count >= 1 && branch2.Count >= 1 && packing.CurrentSequence[i - 1].Equals(packing.CurrentSequence[k]) && FindBranchOccurrences(packing.CurrentSequence, branch1, branch2, i - 1) >= 2)
                    {
                        var newPacking = packing.AddPattern(id => new OrOrPattern(id, "OrOr", branch1, branch2));
                        results.Add(newPacking);
                    }
                }
            }
        }

        return results;
    }

    static int FindBranchOccurrences(List<IElement> sequence, List<IElement> branch1, List<IElement> branch2, int startIndex)
    {
        var occurrences = 0;
        for (var i = startIndex; i < sequence.Count - branch1.Count - branch2.Count - 1; i++)
        {
            if (sequence.Skip(i + 1).Take(branch1.Count).SequenceEqual(branch1, new ElementComparer()) && sequence.Skip(i + 1 + branch1.Count).Take(branch2.Count).SequenceEqual(branch2, new ElementComparer()))
            {
                occurrences++;
            }
        }
        return occurrences;
    }


    static List<PackingVariant> FindRepeatingChunks(PackingVariant packing)
    {
        var results = new List<PackingVariant>();
        for (var i = 0; i < packing.CurrentSequence.Count; i++)
        {
            for (var length = 2; i + length * 2 <= packing.CurrentSequence.Count; length++)
            {
                var sequence = packing.CurrentSequence.GetRange(i, length);
                var restOfTheSequence = packing.CurrentSequence.Skip(i + length).ToList();

                // Проверяем, встречается ли sequence еще раз в оставшейся части списка
                var isRepeated = false;
                for (var j = 0; j <= restOfTheSequence.Count - length; j++)
                {
                    var subSequence = restOfTheSequence.GetRange(j, length);
                    if (sequence.SequenceEqual(subSequence, new ElementComparer()))
                    {
                        isRepeated = true;
                        break;
                    }
                }

                if (isRepeated)
                {
                    var newPacking = packing.AddPattern(id => new SimplePattern(id, sequence, "RepeatingChunk"));
                    results.Add(newPacking);
                }
            }
        }
        return results;
    }
}
class ElementComparer : IEqualityComparer<IElement>
{
    public bool Equals(IElement x, IElement y)
    {
        return x.Equals(y);
    }

    public int GetHashCode(IElement obj)
    {
        return obj.GetHashCode();
    }
}
interface IElement : IEquatable<IElement>
{
}

class SourceElement : IElement
{
    public int Value { get; }

    public SourceElement(int value)
    {
        Value = value;
    }

    // Оператор неявного преобразования из int в SourceElement
    public static implicit operator SourceElement(int value)
    {
        return new(value);
    }

    public bool Equals(IElement? other) => other is SourceElement sourceElement && Value == sourceElement.Value;

    public override int GetHashCode() => Value;

    public override string ToString() => Value.ToString();
}

class PatternElement : IElement
{
    public PatternElement(int id, Pattern pattern)
    {
        Id = id;
        Pattern = pattern;
    }

    public int Id { get; set; }
    public Pattern Pattern { get; }

    public bool Equals(IElement? other) => other is Pattern pattern && Id == pattern.Id;

    public override string ToString() => $"p{Id}";

    public override int GetHashCode() => Id;
}

class OrOrPattern : Pattern
{
    public List<IElement> Branch1 { get; set; }
    public List<IElement> Branch2 { get; set; }

    public OrOrPattern(int id, string description, List<IElement> branch1, List<IElement> branch2)
        : base(id, description)
    {
        Branch1 = branch1;
        Branch2 = branch2;
    }

    public override void ApplyPattern(List<IElement> sequence)
    {
        ApplyPattern(sequence, Branch1, () => new PatternElement(Id, this));
        ApplyPattern(sequence, Branch2, () => new PatternElement(Id, this));
    }

    public override string GetUnpackingTrace() => $"{string.Join(", ", Branch1)} | {string.Join(", ", Branch2)}";
}

class SimplePattern : Pattern
{
    public List<IElement> Unpacking { get; set; } // Содержит элементы IElement

    public SimplePattern(int id, List<IElement> unpacking, string description)
        : base(id, description)
    {
        Unpacking = unpacking;
    }

    public override void ApplyPattern(List<IElement> sequence)
    {
        ApplyPattern(sequence, Unpacking, () => new PatternElement(Id, this));
    }

    public override string GetUnpackingTrace() => string.Join(", ", Unpacking);
}

abstract class Pattern
{
    public string Description { get; set; }

    public int Id { get; set; }

    public Pattern(int id, string description)
    {
        Id = id;
        Description = description;
    }

    public abstract void ApplyPattern(List<IElement> sequence);

    protected static void ApplyPattern(List<IElement> sequence, List<IElement> unpacking, Func<IElement> replacement)
    {
        for (var i = 0; i <= sequence.Count - unpacking.Count; i++)
        {
            // Проверяем, соответствует ли подпоследовательность в sequence паттерну Unpacking
            var subSequence = sequence.GetRange(i, unpacking.Count);
            if (subSequence.SequenceEqual(unpacking, new ElementComparer()))
            {
                // Заменяем подпоследовательность на экземпляр паттерна
                sequence.RemoveRange(i, unpacking.Count);
                sequence.Insert(i, replacement());
                // Продолжаем поиск с текущей позиции, учитывая, что длина sequence изменилась
                i--; // Корректируем индекс i, чтобы учесть изменение длины sequence
            }
        }
    }

    public abstract string GetUnpackingTrace();
}

class PackingVariant
{
    private int _nextPatternId;
    public List<IElement> CurrentSequence { get; set; } = new(); // Теперь содержит элементы IElement
    public List<Pattern> Patterns { get; set; } = new(); // Список паттернов

    public PackingVariant AddPattern(Func<int, Pattern> newPattern)
    {
        var newVariant = new PackingVariant
        {
            _nextPatternId = _nextPatternId + 1,
            CurrentSequence = new(CurrentSequence),
            Patterns = new(Patterns)
        };

        var pattern = newPattern(newVariant._nextPatternId);
        newVariant.Patterns.Add(pattern);

        pattern.ApplyPattern(newVariant.CurrentSequence);

        return newVariant;
    }
}