namespace OneShelf.Videos.Telegram.Processor.Services;

public class Scope
{
    private int? _updateId;

    public int UpdateId => _updateId ?? throw new("Not initialized.");

    public void Initialize(int updateId)
    {
        if (_updateId.HasValue)
            throw new("Already initialized.");

        _updateId = updateId;
    }
}