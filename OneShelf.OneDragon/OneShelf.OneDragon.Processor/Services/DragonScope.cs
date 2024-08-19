namespace OneShelf.OneDragon.Processor.Services;

public class DragonScope
{
    private int? _updateId;
    private long? _chatId;

    public int UpdateId => _updateId ?? throw new("Not initialized.");
    public long ChatId => _chatId ?? throw new("Not initialized.");

    public void Initialize(int updateId, long? chatId)
    {
        if (_updateId.HasValue)
            throw new("Already initialized.");

        _updateId = updateId;
        _chatId = chatId;
    }
}