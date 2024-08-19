namespace OneShelf.OneDragon.Processor.Services;

public class DragonScope
{
    private int? _updateId;
    private long? _chatId;
    private long? _userId;

    public int UpdateId => _updateId ?? throw new("Not initialized.");
    public long ChatId => _chatId ?? throw new("Not initialized.");
    public long UserId => _userId ?? throw new("Not initialized.");

    public void Initialize(int updateId, long? chatId, long? userId)
    {
        if (_updateId.HasValue)
            throw new("Already initialized.");

        _updateId = updateId;
        _chatId = chatId;
        _userId = userId;
    }
}