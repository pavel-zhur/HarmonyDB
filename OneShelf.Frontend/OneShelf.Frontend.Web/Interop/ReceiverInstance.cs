namespace OneShelf.Frontend.Web.Interop;

public class ReceiverInstance : IDisposable
{
    private readonly Action<ReceiverInstance> _onDispose;
    private Func<string, int, Task>? _actionChordClick;

    public ReceiverInstance(Action<ReceiverInstance> onDispose)
    {
        _onDispose = onDispose;
    }

    public void SetChordClick(Func<string, int, Task> action) => _actionChordClick = action;

    public async Task CallChordClick(string chordData, int chordIndex)
    {
        if (_actionChordClick != null) await _actionChordClick.Invoke(chordData, chordIndex);
    }

    public void Dispose()
    {
        _onDispose(this);
    }
}