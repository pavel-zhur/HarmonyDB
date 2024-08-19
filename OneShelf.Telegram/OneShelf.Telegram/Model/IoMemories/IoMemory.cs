namespace OneShelf.Telegram.Model.IoMemories;

public class IoMemory
{
    private readonly List<IoMemoryItem> _events = new();
    private readonly List<string> _inputs = new();

    private int _currentEvent;
    private int _currentInput;

    public IoMemory(long userId, string alias, string? parameters)
    {
        Parameters = parameters;
        UserId = userId;
        Alias = alias;
    }

    public string? Parameters { get; }

    public long UserId { get; }

    public string Alias { get; }

    public string GetNextInput()
    {
        if (_currentInput == _inputs.Count)
        {
            throw new NeedDialogResponseException();
        }

        return _inputs[_currentInput++];
    }

    public void Event(IoMemoryItem ioMemoryItem)
    {
        if (_currentEvent == _events.Count) // new event
        {
            _events.Add(ioMemoryItem);
            _currentEvent++;
        }
        else // existing event
        {
            var existing = _events[_currentEvent++];
            if (!existing.Equals(ioMemoryItem))
            {
                throw new($"Assertion ({UserId}, {Parameters}): {existing} vs. {ioMemoryItem}");
            }
        }
    }

    public void Restart()
    {
        _currentInput = 0;
        _currentEvent = 0;
    }

    public void NewInput(string input)
    {
        _inputs.Add(input);
    }
}