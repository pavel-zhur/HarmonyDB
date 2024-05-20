using HarmonyDB.Index.Analysis.Services;
using Microsoft.JSInterop;
using Nito.AsyncEx;

namespace OneShelf.Frontend.Web.Services;

public class Player : IAsyncDisposable, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<Player> _logger;
    private readonly ChordDataParser _chordDataParser;

    private readonly AsyncLock _moduleLock = new();
    private IJSObjectReference? _module;

    public event EventHandler<(int note, string fingering)?>? ChordPlayed;

    public Player(IJSRuntime jsRuntime, ILogger<Player> logger, ChordDataParser chordDataParser)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _chordDataParser = chordDataParser;
    }

    public async Task PlayChord(string chordData)
    {
        var parsed = _chordDataParser.GetNotes(chordData);
        if (!parsed.HasValue) return;

        if (parsed.Value.fingering != null)
        {
            OnChordPlayed((parsed.Value.root, parsed.Value.fingering));
        }
        else
        {
            OnChordPlayed(null);
        }

        await PlayChord(parsed.Value.bass, parsed.Value.main);
    }

    private async Task PlayChord(int[] bass, int[] main)
    {
        using (await _moduleLock.LockAsync())
        {
            _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./services-js/{GetType().Name}.js");
        }

        await _module.InvokeVoidAsync("playChord", bass, main);
    }

    public void Dispose()
    {
        _module?.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }

    protected virtual void OnChordPlayed((int note, string fingering)? chord)
    {
        ChordPlayed?.Invoke(this, chord);
    }

    public void NoChord()
    {
        OnChordPlayed(null);
    }
}